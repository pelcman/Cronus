// ChannelHandler partial: pickup/drop, item use, scrolling, slots, NPC shops, storage.
using System.Security.Cryptography;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

public sealed partial class ChannelHandler
{
    /// <summary>
    /// Releases the client's exclusive-request lock without changing anything: the empty
    /// LP_InventoryOperation (the oracle's <c>updateInv()</c>). Every inventory-touching request
    /// the client sends locks it until an InventoryOperation or StatChanged answers — a silently
    /// dropped request wedges the client, so every failure branch must send this.
    /// </summary>
    private ValueTask UnlockInventoryAsync(MapleSession session)
        => session.SendAsync(_packets.InventoryOperation(Array.Empty<InventoryChange>()));

    private async ValueTask HandleDropPickUpAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_DropPickUpRequest: [unk:1][updateTime:4][x:2][y:2][objectId:4]
        packet.ReadByte();
        packet.ReadInt();
        packet.ReadShort();
        packet.ReadShort();
        int dropOid = packet.ReadInt();

        await PickUpDropAsync(session, dropOid, petSlot: -1).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_PetDropPickUpRequest</c> — the pet loots for its owner (ports
    /// <c>ReqCUser_Pet.OnPetDropPickUpRequest</c>, JMS ≥ 148 body). The loot lands on the owner;
    /// the field sees the pet-pickup animation.
    /// </summary>
    private async ValueTask HandlePetDropPickUpAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null || packet.Remaining < 17)
        {
            return;
        }

        // [petIndex:4][unk:1][updateTime:4][x:2][y:2][dropOid:4][crc:4][unk:2](+trap fields)
        int petSlot = packet.ReadInt();
        packet.Skip(9);
        int dropOid = packet.ReadInt();

        await PickUpDropAsync(session, dropOid, petSlot).ConfigureAwait(false);
    }

    /// <summary>Takes a drop off the field into the player's purse/inventory; the leave broadcast
    /// carries the pet slot when a pet did the looting (LeaveType 5).</summary>
    private async ValueTask PickUpDropAsync(MapleSession session, int dropOid, int petSlot)
    {
        if (_player!.Character.Hp <= 0)
        {
            await UnlockInventoryAsync(session).ConfigureAwait(false); // the dead don't loot
            return;
        }

        // Full-tab check BEFORE taking the drop off the field, so a full inventory leaves the
        // item on the ground with the "inventory full" toast instead of eating it.
        if (_field!.FindDrop(dropOid) is { IsMeso: false } peek
            && peek.ItemId / 10_000 != 238
            && !Inventory.CanAdd(_player.Character, peek.ItemId, peek.Quantity,
                    _items.GetConsume(peek.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax))
        {
            await session.SendAsync(_packets.ShowInventoryFull()).ConfigureAwait(false);
            return;
        }

        FieldDrop? drop = _field!.RemoveDrop(dropOid);
        if (drop is null)
        {
            await UnlockInventoryAsync(session).ConfigureAwait(false); // already taken
            return;
        }

        Character c = _player.Character;

        // Monster cards (238xxxx) register into the Monster Book instead of the inventory,
        // in the reference's EXACT packet order (ports ReqCDropPool's consumeOnPickup card
        // branch): the book update + effects first, then the drop's leave, then the standard
        // pickup message, and finally the empty InventoryOperation that RELEASES the client's
        // exclusive-request lock (updateInv) — without it the client dies after the pickup.
        if (drop.ItemId / 10_000 == 238)
        {
            int count = c.MonsterCards.TryGetValue(drop.ItemId, out int have) ? have : 0;
            if (count < GameConstants.MonsterCardMaxCount)
            {
                count = Math.Min(GameConstants.MonsterCardMaxCount, count + Math.Max(1, (int)drop.Quantity));
                c.MonsterCards[drop.ItemId] = count;
                _characters.Save(c);
                await session.SendAsync(_packets.MonsterBookSetCard(added: true, drop.ItemId, count)).ConfigureAwait(false);
                await session.SendAsync(_packets.UserEffectLocal(GameConstants.MonsterBookCardEffectValue)).ConfigureAwait(false);
                await session.SendAsync(_packets.ShowCardGain(drop.ItemId)).ConfigureAwait(false);
                await _field.BroadcastAsync(
                    _packets.UserEffectRemote(c.Id, GameConstants.MonsterBookCardEffectValue),
                    exceptCharacterId: c.Id).ConfigureAwait(false);
            }
            else
            {
                await session.SendAsync(_packets.MonsterBookSetCard(added: false, 0, 0)).ConfigureAwait(false);
            }

            byte[] cardLeave = petSlot >= 0
                ? _packets.DropLeaveFieldPetPickup(dropOid, c.Id, petSlot)
                : _packets.DropLeaveFieldPickup(dropOid, c.Id);
            await _field.BroadcastAsync(cardLeave).ConfigureAwait(false);
            await session.SendAsync(_packets.ShowItemGain(drop.ItemId, drop.Quantity)).ConfigureAwait(false);
            await session.SendAsync(_packets.InventoryOperation(Array.Empty<InventoryChange>())).ConfigureAwait(false); // unlock
            return;
        }

        byte[] leave = petSlot >= 0
            ? _packets.DropLeaveFieldPetPickup(dropOid, _player!.Character.Id, petSlot)
            : _packets.DropLeaveFieldPickup(dropOid, _player!.Character.Id);
        await _field.BroadcastAsync(leave).ConfigureAwait(false);

        if (drop.IsMeso)
        {
            c.Meso = (int)Math.Clamp((long)c.Meso + drop.Meso, 0, int.MaxValue);
            _characters.Save(c);
            await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
            await session.SendAsync(_packets.IncMoneyMessage(drop.Meso)).ConfigureAwait(false); // "+N mesos"
            return;
        }

        // Item drop: stack it into the inventory and update the client's slot + show the gain message.
        List<InventoryChange> changes;
        if (drop.ItemInstance is { } instance)
        {
            // A player-thrown equip: restore the exact item (stats intact).
            changes = new List<InventoryChange> { Inventory.Place(c, instance) };
        }
        else
        {
            int slotMax = _items.GetConsume(drop.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax;
            changes = Inventory.Add(c, drop.ItemId, drop.Quantity, slotMax);
            PopulateEquipStats(changes); // a mob-dropped equip gets its wz base stats
        }

        _characters.Save(c);
        if (changes.Count > 0)
        {
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.ShowItemGain(drop.ItemId, drop.Quantity)).ConfigureAwait(false);
    }

    /// <summary>
    /// Fills in the wz base stats (attack/defense/upgrade slots/…) on any newly-created equip in
    /// <paramref name="changes"/>, so a dropped/bought/spawned equip isn't a statless blank. Must run
    /// before the item is serialized into <c>LP_InventoryOperation</c> and saved.
    /// </summary>
    private void PopulateEquipStats(IReadOnlyList<InventoryChange> changes)
    {
        foreach (InventoryChange ch in changes)
        {
            if (ch.Item is not { } item || Inventory.Tab(item.ItemId) != 1)
            {
                continue;
            }

            if (_items.GetEquipStats(item.ItemId) is not { } s)
            {
                continue;
            }

            item.UpgradeSlots = s.UpgradeSlots;
            item.Str = s.Str;
            item.Dex = s.Dex;
            item.Int = s.Int;
            item.Luk = s.Luk;
            item.Hp = s.Hp;
            item.Mp = s.Mp;
            item.Watk = s.Watk;
            item.Matk = s.Matk;
            item.Wdef = s.Wdef;
            item.Mdef = s.Mdef;
            item.Acc = s.Acc;
            item.Avoid = s.Avoid;
            item.Hands = s.Hands;
            item.Speed = s.Speed;
            item.Jump = s.Jump;
        }
    }

    /// <summary>Meso-drop bounds (ports <c>OnUserDropMoneyRequest</c>): a throw is 10..50000 mesos.</summary>
    private const int MinMesoDrop = GameConstants.MesoDropMin;
    private const int MaxMesoDrop = GameConstants.MesoDropMax;

    /// <summary>
    /// Handles <c>CP_UserDropMoneyRequest</c> — a player throws mesos onto the ground for others to
    /// pick up (ports <c>ReqCUser.OnUserDropMoneyRequest</c>). Deducts the mesos and spawns a
    /// player-owned meso drop at their feet; the amount is bounded and must be affordable.
    /// </summary>
    private async ValueTask HandleDropMoneyAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        packet.ReadInt();               // timestamp
        int mesos = packet.ReadInt();

        Character c = _player.Character;
        if (mesos < MinMesoDrop || mesos > MaxMesoDrop || c.Meso < mesos)
        {
            // Reject: resync the client's meso so a rejected throw doesn't desync the UI.
            await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
            return;
        }

        c.Meso -= mesos;
        _characters.Save(c);
        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);

        FieldDrop drop = _field.AddPlayerMesoDrop(mesos, _player.X, _player.Y, c.Id);
        await _field.BroadcastAsync(_packets.DropEnterFieldMeso(drop)).ConfigureAwait(false);
    }

    /// <summary>The USE inventory tab number.</summary>
    private const int UseTab = 2;

    /// <summary>A return scroll's <c>moveTo</c> sentinel for "warp to this map's return field".</summary>
    private const int ReturnToOwnField = 999999999;

    /// <summary>
    /// Handles <c>CP_UserStatChangeItemUseRequest</c> — using a recovery consumable (ports
    /// <c>ReqCUser.OnUserStatChangeItemUseRequest</c> + <c>MapleCharacter.useItem</c>). Validates the
    /// slot, applies the item's HP/MP recovery (flat and %), decrements the stack, and pushes the
    /// inventory change plus the stat change so the icon and bars update live.
    /// </summary>
    private async ValueTask HandleUseItemAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        if (_player.Character.Hp <= 0)
        {
            await UnlockInventoryAsync(session).ConfigureAwait(false); // the dead don't drink
            return;
        }

        packet.ReadInt();                 // timestamp
        short slot = packet.ReadShort();
        int itemId = packet.ReadInt();

        Character c = _player.Character;
        InventoryItem? item = Inventory.ItemAt(c, UseTab, slot);
        if (item is null || item.ItemId != itemId || item.Quantity < 1)
        {
            await UnlockInventoryAsync(session).ConfigureAwait(false); // desync / already gone
            return;
        }

        ConsumeSpec? spec = _items.GetConsume(itemId);

        // Return / teleport scroll (spec/moveTo): consume it and warp to the target map. 999999999
        // means "this map's return field".
        if (spec is not null && spec.MoveTo != 0)
        {
            int target = spec.MoveTo == ReturnToOwnField
                ? (_maps.GetMap(c.MapId)?.ReturnMap ?? 0)
                : spec.MoveTo;
            if (target > 0 && target != ReturnToOwnField)
            {
                InventoryChange? used = Inventory.RemoveFromSlot(c, UseTab, slot, 1);
                _characters.Save(c);
                if (used is { } uch)
                {
                    await session.SendAsync(_packets.InventoryOperation(new[] { uch })).ConfigureAwait(false);
                }

                await MovePlayerToMapAsync(session, target, spawnPortal: 0).ConfigureAwait(false);
            }
            else
            {
                await UnlockInventoryAsync(session).ConfigureAwait(false); // nowhere to go
            }

            return;
        }

        // Apply the recovery effect from wz (flat + percent of max), clamped to the max.
        StatFlag statChange = 0;
        if (spec is not null)
        {
            int hpGain = spec.Hp + (spec.HpRate > 0 ? c.MaxHp * spec.HpRate / 100 : 0);
            int mpGain = spec.Mp + (spec.MpRate > 0 ? c.MaxMp * spec.MpRate / 100 : 0);
            if (hpGain > 0 && c.Hp < c.MaxHp)
            {
                c.Hp = (short)Math.Min(c.MaxHp, c.Hp + hpGain);
                statChange |= StatFlag.Hp;
            }

            if (mpGain > 0 && c.Mp < c.MaxMp)
            {
                c.Mp = (short)Math.Min(c.MaxMp, c.Mp + mpGain);
                statChange |= StatFlag.Mp;
            }
        }

        InventoryChange? change = Inventory.RemoveFromSlot(c, UseTab, slot, 1);
        _characters.Save(c);

        if (change is { } ch)
        {
            await session.SendAsync(_packets.InventoryOperation(new[] { ch })).ConfigureAwait(false);
        }

        if (statChange != 0)
        {
            await session.SendAsync(_packets.StatChanged(c, statChange)).ConfigureAwait(false);
            await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false);
        }

        // Buff potions (spec/pad, speed, …) grant a temporary stat buff for spec/time ms.
        if (spec is not null)
        {
            await ApplyItemBuffAsync(session, spec).ConfigureAwait(false);
        }
    }

    /// <summary>Applies an item's wz buff spec as a temporary stat (registered for expiry).</summary>
    private async ValueTask ApplyItemBuffAsync(MapleSession session, ConsumeSpec spec)
    {
        List<BuffStat> buffs = BuffEffect.FromSpec(spec);
        if (buffs.Count > 0 && _player is not null)
        {
            _buffs.Register(_player.Character.Id, -spec.ItemId, BuffEffect.Mask64(buffs), spec.Time); // state first
            await session.SendAsync(_packets.TemporaryStatSet(buffs)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_UserStatChangeItemCancelRequest</c> — the player right-clicks a buff icon to end
    /// it early (ports <c>ReqCUser.OnUserStatChangeItemCancelRequest</c>): the buff id is the negative
    /// item id, so we recompute that item's stat mask from wz and clear it with
    /// <c>LP_TemporaryStatReset</c>.
    /// </summary>
    private async ValueTask HandleCancelBuffAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        int buffId = packet.ReadInt();       // negative item id
        int itemId = -buffId;

        // The oracle answers EVERY cancel request with TemporaryStatReset, even when it can't
        // resolve the buff (the mask is just 0 then) — the client is waiting on it.
        ulong mask = 0;
        if (itemId > 0 && _items.GetConsume(itemId) is { } spec)
        {
            mask = BuffEffect.Mask64(BuffEffect.FromSpec(spec));
        }

        if (mask != 0)
        {
            _buffs.Remove(_player.Character.Id, buffId);
        }

        await session.SendAsync(_packets.TemporaryStatReset(mask)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserUpgradeItemUseRequest</c> — using an upgrade scroll on an equip (ports
    /// <c>ReqCUser.OnUserUpgradeItemUseRequest</c> + <c>scrollEquipWithId</c>, the pre-BB scope):
    /// success applies the scroll's stats (slot−1, level+1), failure burns a slot unless a white
    /// scroll protects it, and a curse destroys the equip. The field sees the flash; a scrolled
    /// worn equip repaints the avatar.
    /// </summary>
    private async ValueTask HandleUpgradeItemAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186: [updateTime:4][useSlot:2][equipSlot:2][bWhiteScroll:2 optional]
        packet.ReadInt();
        short useSlot = packet.ReadShort();
        short equipSlot = packet.ReadShort();
        bool wantWhiteScroll = packet.Remaining >= 2 && (packet.ReadShort() & 2) != 0;

        Character c = _player.Character;
        InventoryItem? scroll = Inventory.ItemAt(c, 2, useSlot);
        InventoryItem? equip = c.EquippedItems.FirstOrDefault(i => i.Position == equipSlot && i.IsEquip);
        if (scroll is null || equip is null || scroll.ItemId / 10000 != 204
            || _items.GetScroll(scroll.ItemId) is not { } spec)
        {
            await UnlockInventoryAsync(session).ConfigureAwait(false); // desync / not a scroll
            return;
        }

        bool cleanSlate = Scrolling.IsCleanSlate(scroll.ItemId);
        bool chaos = Scrolling.IsChaosScroll(scroll.ItemId);
        if (!cleanSlate && equip.UpgradeSlots < 1)
        {
            await UnlockInventoryAsync(session).ConfigureAwait(false); // nothing left to scroll
            return;
        }

        if (!cleanSlate && !chaos && !Scrolling.CanScroll(scroll.ItemId, equip.ItemId))
        {
            await UnlockInventoryAsync(session).ConfigureAwait(false); // wrong equip family
            return;
        }

        // White-scroll protection consumes one 2340000 alongside the scroll.
        InventoryItem? whiteScroll = wantWhiteScroll
            ? c.EquippedItems.FirstOrDefault(i => i.ItemId == Scrolling.WhiteScrollItemId && i.Position > 0)
            : null;

        int tuc = _items.GetEquipStats(equip.ItemId)?.UpgradeSlots ?? equip.UpgradeSlots;
        ScrollResult result = Scrolling.Apply(equip, scroll.ItemId, spec, tuc, whiteScroll is not null, Random.Shared);

        var changes = new List<InventoryChange>();
        if (Inventory.RemoveFromSlot(c, 2, useSlot, 1) is { } scrollUse)
        {
            changes.Add(scrollUse);
        }

        if (whiteScroll is not null && Inventory.RemoveFromSlot(c, 2, whiteScroll.Position, 1) is { } wsUse)
        {
            changes.Add(wsUse);
        }

        if (result == ScrollResult.Curse)
        {
            c.EquippedItems.Remove(equip);
            changes.Add(new InventoryChange(InvMode.Remove, 1, equipSlot, null, 0));
        }
        else
        {
            // Re-add the (mutated) equip in place so the client repaints its stats.
            changes.Add(new InventoryChange(InvMode.Add, 1, equipSlot, equip, 1));
        }

        _characters.Save(c);
        await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        await _field.BroadcastAsync(_packets.UserItemUpgradeEffect(c.Id, result, legendarySpirit: equipSlot > 0)).ConfigureAwait(false);

        // A worn equip changing (or vanishing) repaints the character for onlookers.
        if (equipSlot < 0 && result != ScrollResult.Fail)
        {
            await _field.BroadcastAsync(_packets.UserAvatarModified(c), exceptCharacterId: c.Id).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_UserChangeSlotPositionRequest</c> — dragging an item between slots: rearrange
    /// within a tab, equip (inventory → equipped, dst &lt; 0), or unequip (equipped → inventory, src
    /// &lt; 0). Ports <c>ReqCUser.OnUserChangeSlotPositionRequest</c>: it moves/swaps the slot and
    /// relays a single <c>LP_InventoryOperation</c> move; an equip change also broadcasts
    /// <c>LP_UserAvatarModified</c> so the field sees the new look. Dropping an item onto the ground
    /// (dst == 0) isn't modelled yet and is ignored. Negative positions are equipped slots.
    /// </summary>
    private async ValueTask HandleChangeSlotPositionAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        const int equipTab = 1;

        packet.ReadInt();               // timestamp
        int tab = packet.ReadByte();
        short src = packet.ReadShort(); // signed; negative = equipped slot
        short dst = packet.ReadShort(); // signed; negative = equip slot
        short qty = packet.ReadShort(); // split/drop quantity

        // dst == 0 drops the item onto the ground for others to pick up.
        if (dst == 0)
        {
            await DropItemToFieldAsync(session, tab, src, qty).ConfigureAwait(false);
            return;
        }

        // Equipped→equipped moves aren't allowed; refuse (with the unlock) rather than desync.
        if (tab == equipTab && src < 0 && dst < 0)
        {
            await UnlockInventoryAsync(session).ConfigureAwait(false);
            return;
        }

        Character c = _player.Character;
        if (Inventory.Move(c, tab, src, dst) is not { } change)
        {
            await UnlockInventoryAsync(session).ConfigureAwait(false); // empty source slot / no-op
            return;
        }

        _characters.Save(c);
        await session.SendAsync(_packets.InventoryOperation(new[] { change })).ConfigureAwait(false);

        // An equip change (a slot went to/from a negative equipped position) repaints the avatar for
        // everyone else in the field.
        if (_field is not null && tab == equipTab && (src < 0 || dst < 0))
        {
            await _field.BroadcastAsync(_packets.UserAvatarModified(c), exceptCharacterId: c.Id).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Drops an item from a slot onto the ground at the player's feet (the <c>dst == 0</c> case of a
    /// slot-change): removes it from the inventory and spawns a player item drop others can pick up.
    /// Equips ride the drop as their actual instance, so their stats survive drop → pickup.
    /// </summary>
    private async ValueTask DropItemToFieldAsync(MapleSession session, int tab, short src, short qty)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        Character c = _player.Character;
        InventoryItem? item = Inventory.ItemAt(c, tab, src);
        if (item is null)
        {
            await UnlockInventoryAsync(session).ConfigureAwait(false); // empty slot
            return;
        }

        int itemId = item.ItemId;
        FieldDrop drop;
        if (tab == 1)
        {
            // Equips move as the whole object so the picked-up item keeps its stats.
            c.EquippedItems.Remove(item);
            _characters.Save(c);
            await session.SendAsync(_packets.InventoryOperation(new[]
            {
                new InventoryChange(InvMode.Remove, tab, src, null, 0),
            })).ConfigureAwait(false);
            drop = _field.AddPlayerItemDrop(itemId, 1, _player.X, _player.Y, c.Id, instance: item);
        }
        else
        {
            int dropQty = qty <= 0 || qty > item.Quantity ? item.Quantity : qty;
            InventoryChange? change = Inventory.RemoveFromSlot(c, tab, src, dropQty);
            _characters.Save(c);
            if (change is { } ch)
            {
                await session.SendAsync(_packets.InventoryOperation(new[] { ch })).ConfigureAwait(false);
            }

            drop = _field.AddPlayerItemDrop(itemId, (short)dropQty, _player.X, _player.Y, c.Id);
        }

        await _field.BroadcastAsync(_packets.DropEnterFieldItem(drop)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserParcelRequest</c> (the home-delivery window ドイ opens). The dialog
    /// shell ports <c>ReqCParcelDlg</c> (SEND = 0x03, CLOSE = 0x08) and the result codes come
    /// from <c>ResCParcelDlg</c>'s table. The SEND payload layout is NOT in the reference (it
    /// blind-ACKs); this decode was recovered from live-client wire captures (2026-08-26):
    /// <c>[tab:1][slot:2][qty:2][meso:4][recipient:str][flag:1]</c> — one attached item slot
    /// (tab 0 = none) plus meso. Delivery is server-side: the recipient collects at ドイ.
    /// </summary>
    private async ValueTask HandleParcelRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || packet.Remaining < 1)
        {
            return;
        }

        byte action = packet.ReadByte();
        if (action == 0x08)
        {
            return; // CLOSE — nothing to tear down server-side (matches the oracle)
        }

        if (action != 0x03)
        {
            return;
        }

        if (_parcels is null || packet.Remaining < 9)
        {
            await session.SendAsync(_packets.ParcelResult(ParcelBadRequest)).ConfigureAwait(false);
            return;
        }

        int tab = packet.ReadByte();
        short slot = packet.ReadShort();
        int qty = packet.ReadShort();
        int meso = packet.ReadInt();
        string recipientName = packet.Remaining >= 2 ? packet.ReadString() : string.Empty;

        Character c = _player.Character;
        Character? recipient = _characters.FindByName(recipientName);
        if (recipient is null)
        {
            await session.SendAsync(_packets.ParcelResult(ParcelBadRecipient)).ConfigureAwait(false);
            return;
        }

        if (recipient.AccountId == c.AccountId)
        {
            await session.SendAsync(_packets.ParcelResult(ParcelSameAccount)).ConfigureAwait(false);
            return;
        }

        InventoryItem? attached = null;
        if (tab is >= 1 and <= 5)
        {
            InventoryItem? item = Inventory.ItemAt(c, tab, slot);
            if (item is null || qty < 1 || (tab != 1 && item.Quantity < qty))
            {
                await session.SendAsync(_packets.ParcelResult(ParcelBadRequest)).ConfigureAwait(false);
                return;
            }

            attached = item;
        }

        if (meso < 0 || meso > c.Meso || (attached is null && meso == 0))
        {
            await session.SendAsync(_packets.ParcelResult(ParcelNoMoney)).ConfigureAwait(false);
            return;
        }

        // Take the goods from the sender: equips and whole stacks travel as the instance so
        // stats survive; a partial bundle splits off a copy (the trunk-deposit pattern).
        var changes = new List<InventoryChange>();
        InventoryItem? shipped = null;
        if (attached is not null)
        {
            if (tab == 1 || qty >= attached.Quantity)
            {
                c.EquippedItems.Remove(attached);
                attached.Position = 0;
                shipped = attached;
                changes.Add(new InventoryChange(InvMode.Remove, tab, slot, null, 0));
            }
            else
            {
                attached.Quantity -= (short)qty;
                shipped = new InventoryItem { ItemId = attached.ItemId, Quantity = (short)qty };
                changes.Add(new InventoryChange(InvMode.Update, tab, slot, attached, attached.Quantity));
            }
        }

        c.Meso -= meso;
        _characters.Save(c);
        _parcels.Save(new ParcelData
        {
            ToCharacterId = recipient.Id,
            FromName = c.Name,
            Meso = meso,
            Item = shipped,
            SentAt = CharacterDataEncoder.FileTimeNow(),
        });

        if (changes.Count > 0)
        {
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await session.SendAsync(_packets.ParcelResult(ParcelSent)).ConfigureAwait(false);
    }

    // ResCParcelDlg result codes (v186).
    private const byte ParcelNoMoney = 0x0C;
    private const byte ParcelBadRequest = 0x0D;
    private const byte ParcelBadRecipient = 0x0E;
    private const byte ParcelSameAccount = 0x0F;
    private const byte ParcelSent = 0x13;

    /// <summary>
    /// Hands every parcel waiting for this character over the counter (the ドイ script's
    /// receive flow): items keep their instances, meso rides along, and delivery stops at the
    /// first parcel that doesn't fit the inventory. Returns (delivered, remaining).
    /// </summary>
    private async ValueTask<(int Delivered, int Remaining)> ReceiveParcelsAsync(MapleSession session)
    {
        if (_parcels is null || _player is null)
        {
            return (0, 0);
        }

        Character c = _player.Character;
        IReadOnlyList<ParcelData> pending = _parcels.LoadFor(c.Id);
        int delivered = 0;
        foreach (ParcelData parcel in pending)
        {
            if (parcel.Item is { } item
                && !Inventory.CanAdd(c, item.ItemId, item.Quantity,
                        _items.GetConsume(item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax))
            {
                break; // no room — this parcel (and the rest) wait for another visit
            }

            var changes = new List<InventoryChange>();
            if (parcel.Item is { } deliver)
            {
                changes.Add(Inventory.Place(c, deliver)); // preserves equip stats
            }

            if (parcel.Meso > 0)
            {
                c.Meso = (int)Math.Clamp((long)c.Meso + parcel.Meso, 0, int.MaxValue);
            }

            _characters.Save(c);
            _parcels.Delete(parcel.Id);
            delivered++;

            if (changes.Count > 0)
            {
                await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
            }

            if (parcel.Meso > 0)
            {
                await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
            }
        }

        return (delivered, pending.Count - delivered);
    }

    /// <summary>Opens an NPC shop for this session: binds it and sends <c>LP_OpenShopDlg</c>.</summary>
    private async ValueTask OpenShopAsync(MapleSession session, Shop shop)
    {
        _openShop = shop;
        await session.SendAsync(_packets.OpenShopDlg(shop, _items)).ConfigureAwait(false);
    }

    // CP_UserShopRequest flags (ports OpsShop, JMS v186): note Close is 4, not 3.
    private const byte ShopReqBuy = 0;
    private const byte ShopReqSell = 1;
    private const byte ShopReqRecharge = 2;
    private const byte ShopReqClose = 4;

    /// <summary>
    /// Handles <c>CP_UserShopRequest</c> — buy / sell / recharge / close on an open NPC shop (ports
    /// <c>ReqCShopDlg</c> + <c>MapleShop</c>, JMS v186). Buy debits meso and adds the item; sell
    /// removes the slot and credits the wz price; every buy/sell replies with a one-byte
    /// <c>LP_ShopResult</c>. Equip buys and rechargeables are deferred (equips need wz base stats).
    /// </summary>
    private async ValueTask HandleShopRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        byte flag = packet.ReadByte();
        if (flag == ShopReqClose)
        {
            _openShop = null;
            return;
        }

        Shop? shop = _openShop;
        if (shop is null)
        {
            return; // no shop open — ignore
        }

        switch (flag)
        {
            case ShopReqBuy:
                await HandleShopBuyAsync(session, shop, packet).ConfigureAwait(false);
                break;
            case ShopReqSell:
                await HandleShopSellAsync(session, packet).ConfigureAwait(false);
                break;
            case ShopReqRecharge:
                await HandleShopRechargeAsync(session, packet).ConfigureAwait(false);
                break;
        }
    }

    private async ValueTask HandleShopBuyAsync(MapleSession session, Shop shop, PacketReader packet)
    {
        // JMS v186 buy body: [shopPos:2 (discarded — matched by id)][itemId:4][quantity:2]
        packet.ReadShort();
        int itemId = packet.ReadInt();
        int quantity = packet.ReadShort();

        ShopItem? entry = shop.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (entry is null || quantity <= 0)
        {
            await session.SendAsync(_packets.ShopResult(ShopResultCode.BuyNoStock)).ConfigureAwait(false);
            return;
        }

        Character c = _player!.Character;

        // Rechargeables (stars/bullets) sell as a full stack for the flat listed price — the
        // oracle ignores the requested quantity entirely (MapleShop.buy: isRechargable →
        // price = item.getPrice(), quantity = slotMax).
        bool rechargeable = ShopItems.IsRechargeable(itemId);
        if (rechargeable)
        {
            quantity = _items.GetConsume(itemId)?.SlotMax ?? Inventory.DefaultSlotMax;
        }

        long price = rechargeable ? entry.Price : (long)entry.Price * quantity;
        if (entry.Price < 0 || c.Meso < price)
        {
            await session.SendAsync(_packets.ShopResult(ShopResultCode.BuyNoMoney)).ConfigureAwait(false);
            return;
        }

        // No room for the whole purchase: decline before any meso/token changes hands.
        if (!Inventory.CanAdd(c, itemId, quantity, _items.GetConsume(itemId)?.SlotMax ?? Inventory.DefaultSlotMax))
        {
            await session.SendAsync(_packets.ShopResult(ShopResultCode.BuyUnknown)).ConfigureAwait(false);
            return;
        }

        // Token-currency entries: pay ReqItemQ of the ReqItem too (ports MapleShop.buy — one
        // bundle per purchase, and the meso price still applies on top).
        List<InventoryChange>? tokenChanges = null;
        if (entry.ReqItem > 0)
        {
            if (quantity >= 2 || CountInventoryItem(c, entry.ReqItem) < entry.ReqItemQ)
            {
                await session.SendAsync(_packets.ShopResult(ShopResultCode.BuyUnknown)).ConfigureAwait(false);
                return;
            }

            tokenChanges = RemoveInventoryQuantity(c, entry.ReqItem, entry.ReqItemQ);
        }

        c.Meso -= (int)price;
        int slotMax = _items.GetConsume(itemId)?.SlotMax ?? Inventory.DefaultSlotMax;
        List<InventoryChange> changes = Inventory.Add(c, itemId, quantity, slotMax);
        PopulateEquipStats(changes); // a bought equip gets its wz base stats
        _characters.Save(c);

        if (tokenChanges is { Count: > 0 })
        {
            await session.SendAsync(_packets.InventoryOperation(tokenChanges)).ConfigureAwait(false);
        }

        if (changes.Count > 0)
        {
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await session.SendAsync(_packets.ShopResult(ShopResultCode.BuySuccess)).ConfigureAwait(false);
    }

    // Mastery skills that raise a rechargeable's stack cap by level x 10 (ports getMasterySkill).
    private const int ClawMastery = 4100000;
    private const int GunMastery = 5200000;

    /// <summary>
    /// Handles the shop recharge (ports <c>MapleShop.recharge</c>): refills a star/bullet stack to
    /// its cap (wz <c>slotMax</c> + mastery-skill bonus) for <c>round(unitPrice × missing)</c> meso.
    /// Recharge reuses the Sell result codes in the reference.
    /// </summary>
    private async ValueTask HandleShopRechargeAsync(MapleSession session, PacketReader packet)
    {
        short slot = packet.ReadShort();
        Character c = _player!.Character;
        InventoryItem? item = Inventory.ItemAt(c, UseTab, slot);
        if (item is null || !ShopItems.IsRechargeable(item.ItemId))
        {
            await session.SendAsync(_packets.ShopResult(ShopResultCode.SellNoStock)).ConfigureAwait(false);
            return;
        }

        int slotMax = _items.GetConsume(item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax;
        int mastery = c.Skills.TryGetValue(item.ItemId / 10000 == 207 ? ClawMastery : GunMastery, out int lvl) ? lvl : 0;
        slotMax += mastery * 10;
        if (item.Quantity >= slotMax)
        {
            // Already full — the client shouldn't ask, but the dialog still waits on a result.
            // (The oracle replies with an enum the v186 init never sets; we pick the defined
            // RechargeUnknown code instead — a deliberate, safe deviation.)
            await session.SendAsync(_packets.ShopResult(ShopResultCode.RechargeUnknown)).ConfigureAwait(false);
            return;
        }

        double unit = _items.GetUnitPrice(item.ItemId) ?? 0;
        int price = (int)Math.Round(unit * (slotMax - item.Quantity));
        if (price > 0 && c.Meso < price)
        {
            await session.SendAsync(_packets.ShopResult(ShopResultCode.SellUnknown)).ConfigureAwait(false);
            return;
        }

        item.Quantity = (short)slotMax;
        c.Meso -= price;
        _characters.Save(c);

        await session.SendAsync(_packets.InventoryOperation(new[]
        {
            new InventoryChange(InvMode.Update, UseTab, slot, item, item.Quantity),
        })).ConfigureAwait(false);
        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await session.SendAsync(_packets.ShopResult(ShopResultCode.SellSuccess)).ConfigureAwait(false);
    }

    private async ValueTask HandleShopSellAsync(MapleSession session, PacketReader packet)
    {
        // JMS v186 sell body: [invSlot:2][itemId:4][quantity:2]
        short slot = packet.ReadShort();
        int itemId = packet.ReadInt();
        int quantity = packet.ReadShort();
        if (quantity <= 0)
        {
            quantity = 1;
        }

        Character c = _player!.Character;
        int tab = Inventory.Tab(itemId);
        InventoryItem? item = Inventory.ItemAt(c, tab, slot);
        if (item is null || item.ItemId != itemId || item.Quantity < quantity)
        {
            await session.SendAsync(_packets.ShopResult(ShopResultCode.SellNoStock)).ConfigureAwait(false);
            return;
        }

        // Sell price is the wz item price; without one (e.g. equips) we can't price it, so refuse
        // rather than destroy the item for nothing.
        int? unit = _items.GetPrice(itemId);
        if (unit is not { } price || price <= 0)
        {
            await session.SendAsync(_packets.ShopResult(ShopResultCode.SellIncorrectRequest)).ConfigureAwait(false);
            return;
        }

        InventoryChange? change = Inventory.RemoveFromSlot(c, tab, slot, quantity);
        c.Meso = (int)Math.Clamp((long)c.Meso + (long)price * quantity, 0, int.MaxValue);
        _characters.Save(c);

        if (change is { } ch)
        {
            await session.SendAsync(_packets.InventoryOperation(new[] { ch })).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await session.SendAsync(_packets.ShopResult(ShopResultCode.SellSuccess)).ConfigureAwait(false);
    }

    /// <summary>The NPC template shown atop the storage window (the reference's default keeper).</summary>
    private const int StorageNpcId = 1012003;

    /// <summary>Flat meso fee charged per storage deposit (ports <c>ReqCTrunkDlg</c>'s 100-meso fee).</summary>
    private const int StorageDepositFee = 100;

    // CP_UserTrunkRequest modes (OpsTrunk, JMS v186).
    private const byte TrunkReqGetItem = 3;
    private const byte TrunkReqPutItem = 4;
    private const byte TrunkReqMoney = 6;
    private const byte TrunkReqClose = 7;

    /// <summary>Opens the player's account storage: binds it and sends <c>LP_TrunkResult</c> (open).</summary>
    private async ValueTask OpenStorageAsync(MapleSession session)
    {
        Storage storage = _storages.Get(_player!.Character.AccountId);
        _openStorage = storage;
        await session.SendAsync(_packets.TrunkOpen(StorageNpcId, storage)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserTrunkRequest</c> — deposit / withdraw / meso / close on the open storage
    /// (ports <c>ReqCTrunkDlg</c> + <c>TacosStorage</c>, JMS v186). Deposit charges a flat 100-meso
    /// fee; meso &gt; 0 withdraws, &lt; 0 deposits. Item objects move between inventory and storage so
    /// equip stats survive the round-trip.
    /// </summary>
    private async ValueTask HandleTrunkRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        byte mode = packet.ReadByte();
        if (mode == TrunkReqClose)
        {
            _openStorage = null;
            return;
        }

        Storage? storage = _openStorage;
        if (storage is null)
        {
            return; // no storage open — ignore
        }

        switch (mode)
        {
            case TrunkReqPutItem:
                await HandleTrunkDepositAsync(session, storage, packet).ConfigureAwait(false);
                break;
            case TrunkReqGetItem:
                await HandleTrunkWithdrawAsync(session, storage, packet).ConfigureAwait(false);
                break;
            case TrunkReqMoney:
                await HandleTrunkMoneyAsync(session, storage, packet).ConfigureAwait(false);
                break;
        }
    }

    private async ValueTask HandleTrunkDepositAsync(MapleSession session, Storage storage, PacketReader packet)
    {
        // JMS v186 deposit body: [invSlot:2][itemId:4][quantity:2]
        short slot = packet.ReadShort();
        int itemId = packet.ReadInt();
        int qty = packet.ReadShort();

        Character c = _player!.Character;
        if (c.Meso < StorageDepositFee)
        {
            await session.SendAsync(_packets.TrunkError(TrunkOp.PutNoMoney)).ConfigureAwait(false);
            return;
        }

        int tab = Inventory.Tab(itemId);
        InventoryItem? item = Inventory.ItemAt(c, tab, slot);
        if (item is null || item.ItemId != itemId || qty < 1 || item.Quantity < qty)
        {
            await session.SendAsync(_packets.TrunkError(TrunkOp.PutIncorrectRequest)).ConfigureAwait(false);
            return;
        }

        if (storage.IsFull)
        {
            await session.SendAsync(_packets.TrunkError(TrunkOp.PutNoSpace)).ConfigureAwait(false);
            return;
        }

        c.Meso -= StorageDepositFee;

        InventoryChange invChange;
        if (tab == 1 || qty >= item.Quantity)
        {
            // Move the whole item object (equip, or an entire bundle stack) — keeps equip stats.
            c.EquippedItems.Remove(item);
            item.Position = 0;
            storage.Items.Add(item);
            invChange = new InventoryChange(InvMode.Remove, tab, slot, null, 0);
        }
        else
        {
            // Split a bundle: reduce the inventory stack, store a new stack.
            item.Quantity -= (short)qty;
            storage.Items.Add(new InventoryItem { ItemId = itemId, Quantity = (short)qty, CharacterId = c.Id });
            invChange = new InventoryChange(InvMode.Update, tab, slot, item, item.Quantity);
        }

        _characters.Save(c);
        _storages.Save(c.AccountId);
        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);     // the fee
        await session.SendAsync(_packets.InventoryOperation(new[] { invChange })).ConfigureAwait(false);
        await session.SendAsync(_packets.TrunkItemResult(TrunkOp.PutSuccess, storage, tab)).ConfigureAwait(false);
    }

    private async ValueTask HandleTrunkWithdrawAsync(MapleSession session, Storage storage, PacketReader packet)
    {
        // JMS v186 withdraw body: [invType:1][storageSlot:1]
        int type = packet.ReadByte();
        int index = packet.ReadByte();

        Character c = _player!.Character;
        List<InventoryItem> categoryItems = storage.Items.Where(i => Inventory.Tab(i.ItemId) == type).ToList();
        if (index < 0 || index >= categoryItems.Count)
        {
            await session.SendAsync(_packets.TrunkError(TrunkOp.GetFailInventoryFull)).ConfigureAwait(false);
            return;
        }

        InventoryItem item = categoryItems[index];
        if (!Inventory.CanAdd(c, item.ItemId, item.Quantity,
                _items.GetConsume(item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax))
        {
            await session.SendAsync(_packets.TrunkError(TrunkOp.GetFailInventoryFull)).ConfigureAwait(false);
            return; // the item stays in storage
        }

        storage.Items.Remove(item);
        InventoryChange addChange = Inventory.Place(c, item); // preserves equip stats / quantity
        _characters.Save(c);
        _storages.Save(c.AccountId);

        await session.SendAsync(_packets.InventoryOperation(new[] { addChange })).ConfigureAwait(false);
        await session.SendAsync(_packets.TrunkItemResult(TrunkOp.GetSuccess, storage, type)).ConfigureAwait(false);
    }

    private async ValueTask HandleTrunkMoneyAsync(MapleSession session, Storage storage, PacketReader packet)
    {
        // JMS v186 meso body: [meso:4 signed] — positive = withdraw, negative = deposit.
        int meso = packet.ReadInt();
        Character c = _player!.Character;

        if (meso > 0)
        {
            if (storage.Meso < meso)
            {
                await ResyncStorageMesoAsync(session, storage).ConfigureAwait(false);
                return;
            }

            storage.Meso -= meso;
            c.Meso = (int)Math.Clamp((long)c.Meso + meso, 0, int.MaxValue);
        }
        else if (meso < 0)
        {
            int amount = -meso;
            if (c.Meso < amount)
            {
                await ResyncStorageMesoAsync(session, storage).ConfigureAwait(false);
                return;
            }

            c.Meso -= amount;
            storage.Meso = (int)Math.Clamp((long)storage.Meso + amount, 0, int.MaxValue);
        }
        else
        {
            await session.SendAsync(_packets.TrunkError(TrunkOp.PutIncorrectRequest)).ConfigureAwait(false);
            return;
        }

        _characters.Save(c);
        _storages.Save(c.AccountId);
        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await session.SendAsync(_packets.TrunkMoneyResult(storage)).ConfigureAwait(false);
    }

    /// <summary>Re-pushes the player's and storage's meso so a rejected transfer doesn't desync the UI.</summary>
    private async ValueTask ResyncStorageMesoAsync(MapleSession session, Storage storage)
    {
        await session.SendAsync(_packets.StatChanged(_player!.Character, StatFlag.Meso)).ConfigureAwait(false);
        await session.SendAsync(_packets.TrunkMoneyResult(storage)).ConfigureAwait(false);
    }

    // CP_UserQuestRequest actions (the client's pre-BB OpsQuest values).
    private const byte QuestReqLostItem = 0;
    private const byte QuestReqAccept = 1;
    private const byte QuestReqComplete = 2;
    private const byte QuestReqResign = 3;
    private const byte QuestReqOpeningScript = 4;
    private const byte QuestReqCompleteScript = 5;
}
