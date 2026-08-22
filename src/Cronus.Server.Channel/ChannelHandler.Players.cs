// ChannelHandler partial: reactors, fame, exp, stats, pets, chat, whisper, messenger, parties.
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
    /// Handles <c>CP_ReactorHit</c> — striking a reactor (ports <c>MapleReactor.hitReactor</c>'s
    /// core path): the hit advances the wz state machine and is shown to the map; reaching a
    /// terminal state breaks the reactor (it vanishes, respawning after its <c>reactorTime</c>)
    /// and runs <c>scripts/reactor/{id}.js</c> if present (rewards, spawns, …).
    /// </summary>
    private async ValueTask HandleReactorHitAsync(PacketReader packet)
    {
        if (_player is null || _field is null || _reactors is null)
        {
            return;
        }

        int objectId = packet.ReadInt();
        packet.ReadInt();               // character position flags
        short stance = packet.ReadShort();

        FieldReactor? reactor = _field.FindReactor(objectId);
        if (reactor is null || reactor.IsDead || _reactors.GetReactor(reactor.ReactorId) is not { } data)
        {
            return;
        }

        bool broke;
        lock (reactor)
        {
            if (reactor.IsDead || data.IsTerminal(reactor.State))
            {
                return; // already spent (or a simultaneous hit beat us to it)
            }

            reactor.State = (byte)data.NextState(reactor.State);
            broke = data.IsTerminal(reactor.State);
        }

        if (broke)
        {
            // Broken: show the final state, then remove it and schedule the respawn.
            reactor.Break(Environment.TickCount64);
            await _field.BroadcastAsync(_packets.ReactorChangeState(reactor, stance)).ConfigureAwait(false);
            await _field.BroadcastAsync(_packets.ReactorLeaveField(reactor)).ConfigureAwait(false);

            await SpawnReactorDropsAsync(reactor).ConfigureAwait(false);

            if (_reactorScripts is not null)
            {
                ChannelPlayer scriptPlayer = CreateScriptPlayer(_player.Session);
                FieldReactor broken = reactor;
                await Task.Run(() => _reactorScripts.Run(broken.ReactorId.ToString(), scriptPlayer)).ConfigureAwait(false);
            }
        }
        else
        {
            await _field.BroadcastAsync(_packets.ReactorChangeState(reactor, stance)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Rolls the broken reactor's <c>reactordrops</c> table and spawns the winners as ground drops
    /// (ports <c>OdinReactorActionManager.dropItems</c>): each row lands with probability
    /// 1/<c>chance</c>, quest-gated rows only while the breaker has that quest started, and the
    /// items fan out around the reactor (x − 12·n, stepping +25) the way the reference spreads them.
    /// </summary>
    private async ValueTask SpawnReactorDropsAsync(FieldReactor reactor)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        Character c = _player.Character;
        var won = new List<ReactorDropEntry>();
        foreach (ReactorDropEntry entry in _reactorDrops.GetDrops(reactor.ReactorId))
        {
            bool questOk = entry.QuestId <= 0 || c.StartedQuests.ContainsKey(entry.QuestId);
            if (questOk && Random.Shared.NextDouble() < 1.0 / Math.Max(1, entry.Chance))
            {
                won.Add(entry);
            }
        }

        short x = (short)(reactor.X - 12 * won.Count);
        foreach (ReactorDropEntry entry in won)
        {
            FieldDrop drop = _field.AddItemDrop(entry.ItemId, 1, x, reactor.Y, source: null);
            await _field.BroadcastAsync(_packets.DropEnterFieldItem(drop)).ConfigureAwait(false);
            x += 25;
        }
    }

    /// <summary>Looks up a learned skill's wz effect for the growth passives (HP/MP increase).</summary>
    private CharacterProgression.EffectResolver EffectResolverFor(Character c)
        => skillId => c.Skills.TryGetValue(skillId, out int level) ? _skills.GetSkillEffect(skillId, level) : null;

    /// <summary>The character's guild, or null when guildless / unknown.</summary>
    private GuildData? GuildOf(Character c) => c.GuildId > 0 ? _guilds.Get(c.GuildId) : null;

    /// <summary>An online player by name across the channel's fields, or null.</summary>
    private FieldPlayer? FindOnlinePlayerByName(string name)
    {
        foreach (Field field in _fields.Fields)
        {
            foreach (FieldPlayer player in field.Players)
            {
                if (string.Equals(player.Character.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return player;
                }
            }
        }

        return null;
    }

    // CP_FuncKeyMappedModified modes (OpsFuncKeyMapped, JMS v186).
    private const int FuncKeyKeyModified = 0;

    /// <summary>
    /// Handles <c>CP_FuncKeyMappedModified</c> — the player rebinds keys (ports
    /// <c>ReqCFuncKeyMappedMan.OnFuncKeyMappedModified</c>). Mode 0 is a key-rebind delta: each entry
    /// sets a key's binding, or clears it when type is 0. The map persists on the character's keymap
    /// (no response packet). The pet-consume-item modes (1/2/3) aren't modelled yet.
    /// </summary>
    private void HandleFuncKeyMapped(PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        int mode = packet.ReadInt();
        if (mode != FuncKeyKeyModified)
        {
            return; // pet-consume item bindings: not modelled
        }

        Keymap keymap = _keymaps.Get(_player.Character.Id);
        int count = packet.ReadInt();
        for (int i = 0; i < count; i++)
        {
            int key = packet.ReadInt();
            byte type = packet.ReadByte();
            int action = packet.ReadInt();
            if (type != 0)
            {
                keymap.Set(key, new KeyBinding(type, action));
            }
            else
            {
                keymap.Remove(key);
            }
        }

        _keymaps.Save(_player.Character.Id);
    }

    private const int MinFameLevel = 15;
    private const int FameCap = 30000;

    /// <summary>
    /// Handles <c>CP_UserGivePopularityRequest</c> — one player rates another's fame up or down
    /// (ports <c>ReqCUser.OnUserGivePopularityRequest</c>). Requires level 15, a different online
    /// target on the same map, and that you haven't famed them yet this session (a simplified stand-in
    /// for the once-per-day limit). On success the target gains/loses a point (clamped to ±30000) and
    /// both players are notified.
    /// </summary>
    private async ValueTask HandleGivePopularityAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        int targetId = packet.ReadInt();
        bool isUp = packet.ReadByte() != 0;

        if (_player.Character.Level < MinFameLevel)
        {
            await session.SendAsync(_packets.GivePopularityError(ChannelPackets.FameErrLevelLow)).ConfigureAwait(false);
            return;
        }

        if (targetId == _player.Character.Id)
        {
            await session.SendAsync(_packets.GivePopularityError(ChannelPackets.FameErrInvalidTarget)).ConfigureAwait(false);
            return;
        }

        FieldPlayer? target = _field.Players.FirstOrDefault(p => p.Character.Id == targetId);
        if (target is null)
        {
            await session.SendAsync(_packets.GivePopularityError(ChannelPackets.FameErrInvalidTarget)).ConfigureAwait(false);
            return;
        }

        if (!_famedCharacterIds.Add(targetId))
        {
            await session.SendAsync(_packets.GivePopularityError(ChannelPackets.FameErrAlreadyToday)).ConfigureAwait(false);
            return;
        }

        Character tc = target.Character;
        int delta = isUp ? 1 : -1;
        tc.Fame = (short)Math.Clamp(tc.Fame + delta, -FameCap, FameCap);
        _characters.Save(tc);

        await session.SendAsync(_packets.GivePopularitySuccess(tc.Name, isUp, tc.Fame)).ConfigureAwait(false);
        await target.Session.SendAsync(_packets.GivePopularityNotify(_player.Character.Name, isUp)).ConfigureAwait(false);
        await target.Session.SendAsync(_packets.StatChanged(tc, StatFlag.Fame)).ConfigureAwait(false);
        await target.Session.SendAsync(_packets.IncPopMessage(delta)).ConfigureAwait(false); // "+1 fame"
    }

    /// <summary>
    /// Handles <c>CP_UserCharacterInfoRequest</c> — clicking another player opens their info window
    /// (ports <c>ReqCUser.OnCharacterInfoRequest</c>). Looks the target up on the same map and replies
    /// <c>LP_CharacterInfo</c>; ignored if they aren't there.
    /// </summary>
    private async ValueTask HandleCharacterInfoAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        packet.ReadInt();               // update time
        int targetId = packet.ReadInt();

        FieldPlayer? target = _field.Players.FirstOrDefault(p => p.Character.Id == targetId);
        if (target is null)
        {
            return;
        }

        await session.SendAsync(_packets.CharacterInfo(target.Character, GuildOf(target.Character))).ConfigureAwait(false);
    }

    private async ValueTask GrantKillExpAsync(int exp)
    {
        exp = (int)(exp * _rates.Exp); // server exp rate applies to kill exp (not quest rewards)
        if (exp <= 0 || _player is null)
        {
            return;
        }

        Party? party = _parties.GetForCharacter(_player.Character.Id);
        if (party is null)
        {
            await GrantExpToAsync(_player, exp).ConfigureAwait(false); // solo: full exp
            return;
        }

        // Split among party members on the same map; the killer gets the largest share.
        int killerId = _player.Character.Id;
        int killerMap = _player.Character.MapId;
        List<FieldPlayer> sameMap = party.Members.Where(m => m.Character.MapId == killerMap).ToList();

        foreach (FieldPlayer member in sameMap)
        {
            int share = CharacterProgression.PartyExpShare(exp, sameMap.Count, isKiller: member.Character.Id == killerId);
            if (share > 0)
            {
                await GrantExpToAsync(member, share).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Adds exp to one player (processing level-ups) and pushes the stat + level-up effect.</summary>
    private async ValueTask GrantExpToAsync(FieldPlayer recipient, int exp)
    {
        Character c = recipient.Character;
        StatFlag changed = CharacterProgression.GainExp(c, exp, EffectResolverFor(c)); // processes level-ups
        _characters.Save(c);
        await TrySendAsync(recipient, _packets.StatChanged(c, changed)).ConfigureAwait(false);
        await TrySendAsync(recipient, _packets.IncExpMessage(exp)).ConfigureAwait(false); // "+N exp"

        // A level-up plays a show effect: the local client triggers its own from the stat change,
        // so only the remote animation (for onlookers in the field) needs broadcasting.
        if (changed.HasFlag(StatFlag.Level) && _field is not null)
        {
            await _field.BroadcastAsync(
                _packets.UserEffectRemote(c.Id, ChannelPackets.UserEffectLevelUp),
                exceptCharacterId: c.Id).ConfigureAwait(false);
            await RefreshPartyWindowAsync(recipient).ConfigureAwait(false); // party window shows the new level

            // Guildmates' G windows show the new level too (ports guildMemberLevelJobUpdate).
            if (c.GuildId > 0)
            {
                await BroadcastToGuildAsync(
                    c.GuildId, _packets.GuildMemberLevelJob(c.GuildId, c.Id, c.Level, c.Job),
                    exceptCharacterId: c.Id).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Handles <c>CP_UserSitRequest</c> — seats the player on a chair (or stands them when the
    /// seat id is -1) and echoes <c>LP_UserSitResult</c>. Sitting makes HP/MP regen fast and
    /// immediate (see <c>PlayerRegenService</c>).
    /// </summary>
    private async ValueTask HandleUserSitAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        short seatId = packet.ReadShort(); // JMS v186 CP_UserSitRequest: [seatId:2] (-1 = stand)
        _player.Seated = seatId != -1;

        // Standing up also leaves a portable chair (ports OnUserSitRequest's cancel branch).
        if (seatId == -1 && _player.PortableChair != 0)
        {
            _player.PortableChair = 0;
            if (_field is not null)
            {
                await _field.BroadcastAsync(
                    _packets.UserSetActivePortableChair(_player.Character.Id, 0),
                    exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
            }
        }

        await session.SendAsync(_packets.UserSitResult(seatId)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserChangeStatRequest</c> — the client's own regen tick (ports
    /// <c>OnUserChangeStatRequest</c>): the claimed HP/MP recovery applies, clamped to max. Kept
    /// modest — the server's own <c>PlayerRegenService</c> is the main regen path.
    /// </summary>
    private async ValueTask HandleChangeStatRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        // JMS v186: [time:4][mask:4][hp:2 if mask&0x400][mp:2 if mask&0x1000][unk:1][time2:4]
        packet.ReadInt();
        int mask = packet.ReadInt();
        short healHp = (mask & 0x400) != 0 ? packet.ReadShort() : (short)0;
        short healMp = (mask & 0x1000) != 0 ? packet.ReadShort() : (short)0;

        Character c = _player.Character;
        if (c.Hp <= 0 || (healHp <= 0 && healMp <= 0))
        {
            return;
        }

        StatFlag changed = 0;
        if (healHp > 0 && c.Hp < c.MaxHp)
        {
            c.Hp = (short)Math.Min(c.MaxHp, c.Hp + healHp);
            changed |= StatFlag.Hp;
        }

        if (healMp > 0 && c.Mp < c.MaxMp)
        {
            c.Mp = (short)Math.Min(c.MaxMp, c.Mp + healMp);
            changed |= StatFlag.Mp;
        }

        if (changed != 0)
        {
            _characters.Save(c);
            await session.SendAsync(_packets.StatChanged(c, changed)).ConfigureAwait(false);
            await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_UserSkillPrepareRequest</c> — a charge skill's windup (ports
    /// <c>OnUserSkillPrepareRequest</c>): verified against the learned level, then mirrored to
    /// onlookers with <c>LP_UserSkillPrepare</c> so they see the charging animation.
    /// </summary>
    private async ValueTask HandleSkillPrepareAsync(PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        int skillId = packet.ReadInt();
        byte level = packet.ReadByte();
        short action = packet.ReadShort(); // JMS >= 186: two bytes
        byte actionSpeed = packet.ReadByte();

        Character c = _player.Character;
        if (!c.Skills.TryGetValue(skillId, out int learned) || learned != level)
        {
            return; // server authority over the claimed level
        }

        await _field.BroadcastAsync(
            _packets.UserSkillPrepare(c.Id, skillId, level, action, actionSpeed),
            exceptCharacterId: c.Id).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_MobApplyCtrl</c> — a hit client asks to steer the mob (ports
    /// <c>OnMobApplyCtrl</c>): granted only when the mob has no live controller.
    /// </summary>
    private async ValueTask HandleMobApplyCtrlAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        int mobOid = packet.ReadInt();
        FieldMob? mob = _field.FindMob(mobOid);
        if (mob is null || mob.IsDead)
        {
            return;
        }

        bool controllerAlive = mob.ControllerId != -1
            && _field.Players.Any(p => p.Character.Id == mob.ControllerId);
        if (!controllerAlive)
        {
            mob.ControllerId = _player.Character.Id;
            await session.SendAsync(_packets.MobChangeController(mob, aggro: true)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_UserConsumeCashItemUseRequest</c> — currently the megaphone family (ports
    /// <c>cashItem507_Megaphone</c>): the line goes to every online player and the megaphone is
    /// consumed. Other cash items are ignored (and kept).
    /// </summary>
    private async ValueTask HandleCashItemUseAsync(PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        // JMS v186: [time:4][cashSlot:2][itemId:4][per-item payload]
        packet.ReadInt();
        short slot = packet.ReadShort();
        int itemId = packet.ReadInt();

        Character c = _player.Character;
        InventoryItem? item = Inventory.ItemAt(c, 5, slot);
        if (item is null || item.ItemId != itemId)
        {
            return;
        }

        // Ad boards (黒板, 537xxxx): stand the message over the player; the board isn't consumed.
        if (itemId / 10000 == 537)
        {
            string boardMessage = packet.ReadString();
            _player.AdBoard = boardMessage;
            if (_field is not null)
            {
                await _field.BroadcastAsync(_packets.UserAdBoard(c.Id, boardMessage)).ConfigureAwait(false);
            }

            return;
        }

        if (itemId / 10000 != 507)
        {
            return;
        }

        byte type;
        string message;
        byte ear = 0;
        switch (itemId)
        {
            case 5070000:
                type = ChannelPackets.MegaphoneChannel;
                message = packet.ReadString();
                break;
            case 5071000:
                type = ChannelPackets.MegaphoneWorld;
                message = packet.ReadString();
                ear = packet.ReadByte();
                break;
            case 5073000:
                type = ChannelPackets.MegaphoneHeart;
                message = packet.ReadString();
                ear = packet.ReadByte();
                break;
            case 5074000:
                type = ChannelPackets.MegaphoneSkull;
                message = packet.ReadString();
                ear = packet.ReadByte();
                break;
            default:
                return; // other megaphone variants (item/triple/avatar) aren't modelled
        }

        InventoryChange? used = Inventory.RemoveFromSlot(c, 5, slot, 1);
        _characters.Save(c);
        if (used is { } uch)
        {
            await _player.Session.SendAsync(_packets.InventoryOperation(new[] { uch })).ConfigureAwait(false);
        }

        byte[] shout = _packets.Megaphone(type, $"{c.Name} : {message}", ear);
        foreach (Field field in _fields.Fields)
        {
            await field.BroadcastAsync(shout).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_UserActivatePetRequest</c> — summoning / dismissing a pet (ports
    /// <c>OnUserActivatePetRequest</c> + <c>spawnPet</c>): the pet spawns at the owner and the
    /// whole map sees it via <c>LP_PetActivated</c>. One pet at a time.
    /// </summary>
    private async ValueTask HandleActivatePetAsync(PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186: [time:4][cashSlot:2][bossFlag:1]
        packet.ReadInt();
        short slot = packet.ReadShort();

        Character c = _player.Character;
        InventoryItem? item = Inventory.ItemAt(c, 5, slot);
        if (item is null || !Cronus.Server.Login.ItemEncoder.IsPet(item.ItemId))
        {
            return;
        }

        if (_player.Pet is { } current && current.Item == item)
        {
            // Same pet again = dismiss.
            _player.Pet = null;
            await _field.BroadcastAsync(_packets.PetDeactivated(c.Id)).ConfigureAwait(false);
            return;
        }

        _player.Pet = new ActivePet(item, _player.X, _player.Y);
        await _field.BroadcastAsync(_packets.PetActivated(c.Id, _player.Pet)).ConfigureAwait(false);
    }

    /// <summary>Handles <c>CP_PetMove</c> — relays the pet's path to onlookers (ports <c>OnPetMove</c>).</summary>
    private async ValueTask HandlePetMoveAsync(PacketReader packet)
    {
        if (_player is null || _field is null || _player.Pet is not { } pet)
        {
            return;
        }

        packet.ReadInt(); // pet index
        byte[] path = packet.ReadRemaining();
        if (path.Length >= 4)
        {
            pet.X = (short)(path[0] | (path[1] << 8));
            pet.Y = (short)(path[2] | (path[3] << 8));
        }

        await _field.BroadcastAsync(
            _packets.PetMove(_player.Character.Id, path),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
    }

    /// <summary>Handles <c>CP_PetAction</c> — pet emotes/speech to onlookers (ports <c>OnPetAction</c>).</summary>
    private async ValueTask HandlePetActionAsync(PacketReader packet)
    {
        if (_player is null || _field is null || _player.Pet is null)
        {
            return;
        }

        packet.ReadInt(); // pet index
        byte type = packet.ReadByte();
        byte action = packet.ReadByte();
        string message = packet.ReadString();
        await _field.BroadcastAsync(
            _packets.PetAction(_player.Character.Id, type, action, message),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserPetFoodItemUseRequest</c> — feeding the pet (ports <c>OnPetFood</c>,
    /// simplified): the food is consumed, fullness refills, and closeness grows on the pet item.
    /// </summary>
    private async ValueTask HandlePetFoodAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _player.Pet is not { } pet)
        {
            return;
        }

        packet.ReadInt();
        short slot = packet.ReadShort();
        int itemId = packet.ReadInt();

        Character c = _player.Character;
        InventoryItem? food = Inventory.ItemAt(c, 2, slot);
        if (food is null || food.ItemId != itemId)
        {
            return;
        }

        int incFullness = _items.GetConsume(itemId)?.Hp is > 0 and var inc ? inc : 30; // spec/inc fallback
        pet.Item.PetFullness = (byte)Math.Min(100, pet.Item.PetFullness + Math.Max(10, incFullness));
        pet.Item.PetCloseness = (short)Math.Min(30000, pet.Item.PetCloseness + 10);

        var changes = new List<InventoryChange>();
        if (Inventory.RemoveFromSlot(c, 2, slot, 1) is { } used)
        {
            changes.Add(used);
        }

        // Re-add the pet item in place so the client refreshes closeness/fullness.
        changes.Add(new InventoryChange(InvMode.Add, 5, pet.Item.Position, pet.Item, 1));
        _characters.Save(c);
        await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserPortableChairSitRequest</c> — sitting on a portable chair from the SETUP
    /// tab (ports <c>OnUserPortableChairSitRequest</c>): the map sees the chair; standing (a sit
    /// request with -1) clears it. Fishing chairs' timed rewards aren't modelled.
    /// </summary>
    private async ValueTask HandlePortableChairAsync(PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        int itemId = packet.ReadInt();
        if (CountInventoryItem(_player.Character, itemId) < 1 || itemId / 1000000 != 3)
        {
            return; // must own the chair (SETUP item)
        }

        _player.Seated = true;
        _player.PortableChair = itemId;
        await _field.BroadcastAsync(
            _packets.UserSetActivePortableChair(_player.Character.Id, itemId),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserMacroSysDataModified</c> — the player saved their skill macros (ports
    /// <c>ReqCFuncKeyMappedMan</c>): [count][name][shout][skill×3] rows persist on the character
    /// and replay on the next login via <c>LP_MacroSysDataInit</c>.
    /// </summary>
    private void HandleMacroModified(PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        int count = packet.ReadByte();
        c.SkillMacros.Clear();
        for (int i = 0; i < count && i < 5; i++)
        {
            string name = packet.ReadString();
            byte shout = packet.ReadByte();
            int skill1 = packet.ReadInt();
            int skill2 = packet.ReadInt();
            int skill3 = packet.ReadInt();
            c.SkillMacros[i] = new SkillMacroEntry(name, shout, skill1, skill2, skill3);
        }

        _characters.Save(c);
    }

    private async ValueTask HandleUserEmotionAsync(PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_UserEmotion: [emotionId:4]. Basic emotes are 1..7; item-based face
        // expressions (>7) need the item (not modelled here), but relaying one is harmless.
        int emotion = packet.ReadInt();
        if (emotion <= 0)
        {
            return;
        }

        await _field.BroadcastAsync(
            _packets.UserEmotion(_player.Character.Id, emotion),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
    }

    private async ValueTask HandleUserChatAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186: [timestamp:4][message:str][onlyBalloon:1]
        packet.ReadInt();
        string message = packet.ReadString();
        bool onlyBalloon = packet.Remaining > 0 && packet.ReadBool();

        // Commands (prefix '/') are handled server-side and not broadcast.
        if (message.StartsWith('/'))
        {
            await HandleCommandAsync(session, message[1..]).ConfigureAwait(false);
            return;
        }

        byte[] chat = _packets.UserChat(
            _player.Character.Id, isGm: false, message, onlyBalloon);
        await _field.BroadcastAsync(chat).ConfigureAwait(false);
    }

    // CP_Whisper operation bits (ports Ops_Whisper): the client ORs WP_Request(0x04) onto the
    // location/whisper op; strip it to recover which one was asked for.
    private const int WpRequest = 0x04;
    private const int WpLocationOp = 0x01;
    private const int WpWhisperOp = 0x02;

    /// <summary>
    /// Handles <c>CP_Whisper</c> — both a private message (WP_Whisper) and a "/find" location
    /// lookup (WP_Location). Finds the target on this channel by name and routes the message /
    /// answers the lookup (ports <c>ReqCUser.OnWhisper</c>).
    /// </summary>
    private async ValueTask HandleWhisperAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        int operation = packet.ReadByte();
        int op = operation & ~WpRequest; // drop the WP_Request bit

        if (op == WpLocationOp)
        {
            string targetName = packet.ReadString();
            (FieldPlayer Player, int ChannelId)? found = FindWorldPlayerByName(targetName);
            byte[] answer = found switch
            {
                null => _packets.WhisperLocationResult(targetName, 0, online: false),
                var (target, channel) when channel == _channelId =>
                    _packets.WhisperLocationResult(targetName, target.Character.MapId, online: true),
                var (_, channel) => _packets.WhisperLocationOtherChannel(targetName, channel + 1),
            };
            await session.SendAsync(answer).ConfigureAwait(false);
            return;
        }

        if (op == WpWhisperOp)
        {
            string targetName = packet.ReadString();
            string message = packet.ReadString();
            FieldPlayer? target = FindWorldPlayerByName(targetName)?.Player;

            // Ack the sender: was it delivered?
            await session.SendAsync(_packets.WhisperResult(targetName, target is not null))
                .ConfigureAwait(false);

            // Deliver to the recipient (skip when they whisper themselves — the ack is enough).
            if (target is not null && target.Character.Id != _player.Character.Id)
            {
                await target.Session.SendAsync(
                    _packets.WhisperReceive(_player.Character.Name, _channelId, message))
                    .ConfigureAwait(false);
            }
        }
    }

    // CP_Messenger sub-operations the client sends (ports OpsMessenger).
    private const int MsmpEnterOp = 0;
    private const int MsmpLeaveOp = 2;
    private const int MsmpInviteOp = 3;
    private const int MsmpChatOp = 6;

    /// <summary>
    /// Handles <c>CP_Messenger</c> — the 3-person messenger window: create/join (Enter), leave,
    /// invite a player, and chat (ports <c>ReqCUIMessenger.OnMessenger</c> + <c>TacosMessenger</c>).
    /// Block-list and avatar-refresh ops are out of scope (no block/appearance systems yet).
    /// </summary>
    private async ValueTask HandleMessengerAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        int op = packet.ReadByte();
        int myId = _player.Character.Id;
        Messenger? current = _messengers.GetFor(myId);

        switch (op)
        {
            case MsmpEnterOp:
            {
                if (current is not null)
                {
                    return; // already in a messenger
                }

                int messengerId = packet.ReadInt();
                Messenger? target = messengerId == 0 ? _messengers.Create() : _messengers.FindById(messengerId);
                if (target is null)
                {
                    return; // invite expired / bad id
                }

                if (await target.EnterAsync(_player, _channelId).ConfigureAwait(false))
                {
                    _messengers.Register(myId, target);
                }

                return;
            }

            case MsmpLeaveOp:
            {
                if (current is null)
                {
                    return;
                }

                await current.LeaveAsync(myId).ConfigureAwait(false);
                _messengers.Unregister(myId, current);
                return;
            }

            case MsmpInviteOp:
            {
                if (current is null)
                {
                    return;
                }

                string inviteeName = packet.ReadString();
                FieldPlayer? invitee = _fields.FindPlayerByName(inviteeName);
                // Available only if online and not already in a messenger.
                bool available = invitee is not null && _messengers.GetFor(invitee.Character.Id) is null;

                await current.BroadcastInviteResultAsync(inviteeName, available).ConfigureAwait(false);
                if (available)
                {
                    await invitee!.Session.SendAsync(
                        _packets.MessengerInvite(_player.Character.Name, _channelId, current.Id)).ConfigureAwait(false);
                }

                return;
            }

            case MsmpChatOp:
            {
                if (current is null)
                {
                    return;
                }

                string message = packet.ReadString();
                await current.ChatAsync(myId, message).ConfigureAwait(false);
                return;
            }
        }
    }

    // CP_PartyRequest sub-operations the client sends (ports OpsParty).
    private const int PartyOpCreate = 1;
    private const int PartyOpWithdraw = 2;
    private const int PartyOpJoin = 3;
    private const int PartyOpInvite = 4;
    private const int PartyOpKick = 5;
    private const int PartyOpChangeLeader = 6;

    /// <summary>The 1-based party channel (the reference numbers channels from 1; Cronus from 0).</summary>
    private int PartyChannel => _channelId + 1;

    /// <summary>
    /// Handles <c>CP_PartyRequest</c> — create, invite, join, leave/disband, expel, and change
    /// leader (ports <c>ReqCUser.OnPartyRequest</c> + <c>OdinWorld.Party.updateParty</c>). Parties
    /// are in-memory and online-only; exp sharing and party HP bars are follow-ups.
    /// </summary>
    /// <summary>Joins a party by id (the CP_PartyRequest join op and CP_PartyResult accept).</summary>
    private async ValueTask JoinPartyAsync(MapleSession session, int partyId)
    {
        int myId = _player!.Character.Id;
        if (_parties.GetForCharacter(myId) is not null)
        {
            await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrAlreadyInParty)).ConfigureAwait(false);
            return;
        }

        Party? target = _parties.GetById(partyId);
        if (target is null)
        {
            await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrJoinUnknown)).ConfigureAwait(false);
            return;
        }

        if (!target.TryAdd(_player))
        {
            await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrFull)).ConfigureAwait(false);
            return;
        }

        _parties.Register(myId, target);
        byte[] joinPacket = _packets.PartyJoin(target.Id, _player.Character.Name, target.ViewSlots(), target.LeaderId, PartyChannel);
        await PartyBroadcastAsync(target, joinPacket).ConfigureAwait(false);
        await SyncPartyHpAsync(target, _player).ConfigureAwait(false);
    }

    // CP_PartyResult invite-answer values (OpsParty, JMS >= 147).
    private const byte PartyResInviteRejected = 23;
    private const byte PartyResInviteAccepted = 24;

    /// <summary>
    /// Handles <c>CP_PartyResult</c> — the invitee's answer to a party invite (ports
    /// <c>ReqCUser.OnPartyResult</c>): accepting joins the party; a decline is consumed.
    /// </summary>
    private async ValueTask HandlePartyResultAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        byte type = packet.ReadByte();
        int partyId = packet.ReadInt();
        if (type == PartyResInviteAccepted)
        {
            await JoinPartyAsync(session, partyId).ConfigureAwait(false);
        }
        // A decline (23) is consumed silently, matching the reference.
    }

    private async ValueTask HandlePartyRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        int type = packet.ReadByte();
        int myId = _player.Character.Id;
        Party? party = _parties.GetForCharacter(myId);

        switch (type)
        {
            case PartyOpCreate:
            {
                if (party is not null)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrAlreadyJoined)).ConfigureAwait(false);
                    return;
                }

                Party created = _parties.Create(_player);
                await session.SendAsync(_packets.PartyCreateDone(created.Id)).ConfigureAwait(false);
                return;
            }

            case PartyOpWithdraw:
            {
                if (party is null)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrWithdrawUnknown)).ConfigureAwait(false);
                    return;
                }

                await LeavePartyAsync(party, _player, byDisconnect: false).ConfigureAwait(false);
                return;
            }

            case PartyOpJoin:
            {
                int partyId = packet.ReadInt();
                await JoinPartyAsync(session, partyId).ConfigureAwait(false);
                return;
            }

            case PartyOpInvite:
            {
                string inviteeName = packet.ReadString();
                if (party is null || party.IsFull)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrFull)).ConfigureAwait(false);
                    return;
                }

                FieldPlayer? invitee = _fields.FindPlayerByName(inviteeName);
                if (invitee is null)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrUnknownUser)).ConfigureAwait(false);
                    return;
                }

                if (_parties.GetForCharacter(invitee.Character.Id) is not null)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrAlreadyInParty)).ConfigureAwait(false);
                    return;
                }

                await session.SendAsync(_packets.PartyInviteSent(inviteeName)).ConfigureAwait(false);
                await invitee.Session.SendAsync(
                    _packets.PartyInvite(party.Id, _player.Character.Name, _player.Character.Level, _player.Character.Job)).ConfigureAwait(false);
                return;
            }

            case PartyOpKick:
            {
                int kickId = packet.ReadInt();
                if (party is null || !party.IsLeader(myId))
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrKickUnknown)).ConfigureAwait(false);
                    return;
                }

                FieldPlayer? kicked = party.MemberById(kickId);
                if (kicked is null || kickId == myId)
                {
                    return; // can't kick a non-member or yourself
                }

                party.Remove(kickId);
                _parties.Unregister(kickId);
                byte[] expel = _packets.PartyDepart(party.Id, kickId, kicked.Character.Name, PartyDepart.Expel, party.ViewSlots(), party.LeaderId, PartyChannel);
                await PartyBroadcastAsync(party, expel).ConfigureAwait(false);
                await TrySendAsync(kicked, expel).ConfigureAwait(false);
                return;
            }

            case PartyOpChangeLeader:
            {
                int newLeaderId = packet.ReadInt();
                if (party is null || !party.IsLeader(myId) || !party.Contains(newLeaderId))
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrChangeBossUnknown)).ConfigureAwait(false);
                    return;
                }

                party.SetLeader(newLeaderId);
                byte[] change = _packets.PartyChangeLeader(newLeaderId, byDisconnect: false);
                await PartyBroadcastAsync(party, change).ConfigureAwait(false);
                return;
            }
        }
    }

    /// <summary>
    /// Removes a member from their party: the leader leaving disbands it (everyone is notified),
    /// a member leaving notifies the rest and the leaver. Shared by the withdraw op and disconnect.
    /// </summary>
    private async ValueTask LeavePartyAsync(Party party, FieldPlayer leaver, bool byDisconnect)
    {
        int leaverId = leaver.Character.Id;
        string leaverName = leaver.Character.Name;

        if (party.IsLeader(leaverId))
        {
            // Disband: notify all members while they're still listed, then drop the party.
            byte[] disband = _packets.PartyDepart(party.Id, leaverId, leaverName, PartyDepart.Disband, party.ViewSlots(), party.LeaderId, PartyChannel);
            await PartyBroadcastAsync(party, disband).ConfigureAwait(false);
            _parties.Disband(party);
            return;
        }

        party.Remove(leaverId);
        _parties.Unregister(leaverId);
        byte[] leave = _packets.PartyDepart(party.Id, leaverId, leaverName, PartyDepart.Leave, party.ViewSlots(), party.LeaderId, PartyChannel);
        await PartyBroadcastAsync(party, leave).ConfigureAwait(false); // remaining members
        if (!byDisconnect)
        {
            await TrySendAsync(leaver, leave).ConfigureAwait(false);   // and the leaver's own window
        }
    }

    private static async ValueTask PartyBroadcastAsync(Party party, byte[] packet)
    {
        foreach (FieldPlayer member in party.Members)
        {
            await TrySendAsync(member, packet).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Pushes a player's HP bar (<c>LP_UserHP</c>) to their same-map party members so partners and
    /// healers see it change. No-op outside a party (ports <c>MapleCharacter.updatePartyMemberHP</c>).
    /// </summary>
    private async ValueTask NotifyPartyOfMyHpAsync(FieldPlayer who)
    {
        Party? party = _parties.GetForCharacter(who.Character.Id);
        if (party is null)
        {
            return;
        }

        Character me = who.Character;
        byte[] hp = _packets.UserHP(me.Id, me.Hp, me.MaxHp);
        foreach (FieldPlayer member in party.Members)
        {
            if (member.Character.Id != me.Id && member.Character.MapId == me.MapId)
            {
                await TrySendAsync(member, hp).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Marks a disconnecting member offline: the party survives (they can come back on any
    /// channel) and the others see them grey out. The last member dropping dissolves the party.
    /// </summary>
    private async ValueTask PartyMemberWentOfflineAsync(Party party, int characterId)
    {
        if (!party.MarkOffline(characterId))
        {
            return;
        }

        if (party.AllOffline)
        {
            _parties.Disband(party);
            return;
        }

        byte[] refresh = _packets.PartyRefresh(party.Id, party.ViewSlots(), party.LeaderId, PartyChannel, loading: false);
        foreach (FieldPlayer member in party.Members)
        {
            await TrySendAsync(member, refresh).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Rebroadcasts the party window to all members so a member's changed map or level shows up
    /// (the silent-update op; ports the <c>SILENT_UPDATE</c> path). No-op outside a party.
    /// </summary>
    private async ValueTask RefreshPartyWindowAsync(FieldPlayer member)
    {
        Party? party = _parties.GetForCharacter(member.Character.Id);
        if (party is null)
        {
            return;
        }

        byte[] refresh = _packets.PartyRefresh(party.Id, party.ViewSlots(), party.LeaderId, PartyChannel, loading: false);
        await PartyBroadcastAsync(party, refresh).ConfigureAwait(false);
    }

    /// <summary>
    /// On a join, exchanges HP bars between the joiner and their same-map party members so both
    /// sides' windows start correct (ports <c>updatePartyMemberHP</c> + <c>receivePartyMemberHP</c>).
    /// </summary>
    private async ValueTask SyncPartyHpAsync(Party party, FieldPlayer joiner)
    {
        Character jc = joiner.Character;
        byte[] joinerHp = _packets.UserHP(jc.Id, jc.Hp, jc.MaxHp);
        foreach (FieldPlayer member in party.Members)
        {
            if (member.Character.Id == jc.Id || member.Character.MapId != jc.MapId)
            {
                continue;
            }

            await TrySendAsync(member, joinerHp).ConfigureAwait(false);            // member sees joiner
            Character mc = member.Character;
            await TrySendAsync(joiner, _packets.UserHP(mc.Id, mc.Hp, mc.MaxHp)).ConfigureAwait(false); // joiner sees member
        }
    }

    private static async ValueTask TrySendAsync(FieldPlayer player, byte[] packet)
    {
        try
        {
            await player.Session.SendAsync(packet).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A dead session drops out on its own disconnect path; keep fanning out.
        }
    }
}
