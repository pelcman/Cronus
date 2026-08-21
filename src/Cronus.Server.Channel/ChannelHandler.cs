using System.Security.Cryptography;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

/// <summary>
/// Handles channel-stage packets for one connection (ports <c>PacketHandler_Game</c> paths for
/// JMS v186). Flow: <c>CP_MigrateIn</c> loads the character, replies <c>LP_SetField</c>, and
/// joins the character's field; afterwards movement (<c>CP_UserMove</c>) and chat
/// (<c>CP_UserChat</c>) relay to everyone else in the same field, and disconnecting announces
/// <c>LP_UserLeaveField</c>.
/// </summary>
public sealed class ChannelHandler : PacketHandlerBase
{
    /// <summary>
    /// Byte length of the JMS v186 CP_UserMove prefix before the CMovePath buffer:
    /// int(-1) ×2, one byte, int(-1) ×2 + int ×2, then one trailing int (JMS >= 164).
    /// </summary>
    private const int MovePrefixLength = 4 + 4 + 1 + (4 * 4) + 4;

    /// <summary>LP_TransferFieldReqIgnored reason: the portal is disabled.</summary>
    private const byte TransferDisabledPortal = 1;

    private readonly ChannelPackets _packets;
    private readonly ICharacterRepository _characters;
    private readonly FieldRegistry _fields;
    private readonly IMapProvider _maps;
    private readonly ISkillProvider _skills;
    private readonly IItemProvider _items;
    private readonly IDropProvider _dropTable;
    private readonly IShopProvider _shops;
    private readonly StorageRegistry _storages;
    private readonly KeymapRegistry _keymaps;
    private readonly IQuestProvider _quests;
    private readonly Rates _rates;
    private readonly TradeRegistry _trades;
    private readonly BuffTracker _buffs;
    private readonly NpcScriptEngine? _npcScripts;
    private readonly PortalScriptEngine? _portalScripts;
    private readonly MessengerRegistry _messengers;
    private readonly PartyRegistry _parties;
    private readonly int _channelId;

    private readonly int _opMigrateIn;
    private readonly int _opAliveAck;
    private readonly int _opUserMove;
    private readonly int _opUserChat;
    private readonly int _opUserEmotion;
    private readonly int _opUserSit;
    private readonly int _opMeleeAttack;
    private readonly int _opMagicAttack;
    private readonly int _opShootAttack;
    private readonly int _opUserHit;
    private readonly int _opDropPickUp;
    private readonly int _opDropMoney;
    private readonly int _opUseItem;
    private readonly int _opCancelBuff;
    private readonly int _opChangeSlot;
    private readonly int _opShopRequest;
    private readonly int _opTrunkRequest;
    private readonly int _opFuncKeyMapped;
    private readonly int _opQuestRequest;
    private readonly int _opMiniRoom;
    private readonly int _opFriend;
    private readonly int _opGivePopularity;
    private readonly int _opCharacterInfo;
    private readonly int _opSkillUp;
    private readonly int _opSkillUse;
    private readonly int _opSkillCancel;
    private readonly int _opAbilityUp;
    private readonly int _opAbilityMassUp;
    private readonly int _opMobMove;
    private readonly int _opTransferField;
    private readonly int _opSelectNpc;
    private readonly int _opScriptAnswer;
    private readonly int _opPortalScript;
    private readonly int _opWhisper;
    private readonly int _opMessenger;
    private readonly int _opPartyRequest;

    private FieldPlayer? _player;
    private Field? _field;
    private NpcConversation? _conversation;

    /// <summary>The NPC shop the player currently has open, or null; scoped to this session.</summary>
    private Shop? _openShop;

    /// <summary>The account storage the player currently has open, or null; scoped to this session.</summary>
    private Storage? _openStorage;

    /// <summary>Character ids this player has famed this session (a simplified daily limit).</summary>
    private readonly HashSet<int> _famedCharacterIds = new();

    public ChannelHandler(
        OpcodeTable clientOpcodes,
        OpcodeTable serverOpcodes,
        ICharacterRepository characters,
        ServerConfig config,
        FieldRegistry? fields = null,
        IMapProvider? maps = null,
        NpcScriptEngine? npcScripts = null,
        ISkillProvider? skills = null,
        int channelId = 0,
        MessengerRegistry? messengers = null,
        PartyRegistry? parties = null,
        PortalScriptEngine? portalScripts = null,
        IItemProvider? items = null,
        IDropProvider? drops = null,
        IShopProvider? shops = null,
        StorageRegistry? storages = null,
        KeymapRegistry? keymaps = null,
        IQuestProvider? quests = null,
        Rates? rates = null,
        TradeRegistry? trades = null,
        BuffTracker? buffs = null)
    {
        _packets = new ChannelPackets(serverOpcodes, config);
        _characters = characters;
        _fields = fields ?? new FieldRegistry();
        _maps = maps ?? new InMemoryMapProvider(Array.Empty<MapData>());
        _skills = skills ?? NullSkillProvider.Instance;
        _items = items ?? new InMemoryItemProvider(Array.Empty<ConsumeSpec>());
        _dropTable = drops ?? new InMemoryDropProvider(new Dictionary<int, IReadOnlyList<DropEntry>>());
        _shops = shops ?? new InMemoryShopProvider(Array.Empty<Shop>());
        _storages = storages ?? new StorageRegistry();
        _keymaps = keymaps ?? new KeymapRegistry();
        _quests = quests ?? new InMemoryQuestProvider(Array.Empty<QuestData>());
        _rates = rates ?? Rates.Default;
        _trades = trades ?? new TradeRegistry();
        _buffs = buffs ?? new BuffTracker();
        _npcScripts = npcScripts;
        _portalScripts = portalScripts;
        _channelId = channelId;
        _messengers = messengers ?? new MessengerRegistry(_packets);
        _parties = parties ?? new PartyRegistry();

        _opMigrateIn = clientOpcodes.Get(ClientOpcode.MigrateIn);
        _opAliveAck = clientOpcodes.Get(ClientOpcode.AliveAck);
        _opUserMove = clientOpcodes.Get(ClientOpcode.UserMove);
        _opUserChat = clientOpcodes.Get(ClientOpcode.UserChat);
        _opUserEmotion = clientOpcodes.Get(ClientOpcode.UserEmotion);
        _opUserSit = clientOpcodes.Get(ClientOpcode.UserSitRequest);
        _opMeleeAttack = clientOpcodes.Get(ClientOpcode.UserMeleeAttack);
        _opMagicAttack = clientOpcodes.Get(ClientOpcode.UserMagicAttack);
        _opShootAttack = clientOpcodes.Get(ClientOpcode.UserShootAttack);
        _opUserHit = clientOpcodes.Get(ClientOpcode.UserHit);
        _opDropPickUp = clientOpcodes.Get(ClientOpcode.DropPickUpRequest);
        _opDropMoney = clientOpcodes.Get(ClientOpcode.UserDropMoneyRequest);
        _opUseItem = clientOpcodes.Get(ClientOpcode.UserStatChangeItemUseRequest);
        _opCancelBuff = clientOpcodes.Get(ClientOpcode.UserStatChangeItemCancelRequest);
        _opChangeSlot = clientOpcodes.Get(ClientOpcode.UserChangeSlotPositionRequest);
        _opShopRequest = clientOpcodes.Get(ClientOpcode.UserShopRequest);
        _opTrunkRequest = clientOpcodes.Get(ClientOpcode.UserTrunkRequest);
        _opFuncKeyMapped = clientOpcodes.Get(ClientOpcode.FuncKeyMappedModified);
        _opQuestRequest = clientOpcodes.Get(ClientOpcode.UserQuestRequest);
        _opMiniRoom = clientOpcodes.Get(ClientOpcode.MiniRoom);
        _opFriend = clientOpcodes.Get(ClientOpcode.FriendRequest);
        _opGivePopularity = clientOpcodes.Get(ClientOpcode.UserGivePopularityRequest);
        _opCharacterInfo = clientOpcodes.Get(ClientOpcode.UserCharacterInfoRequest);
        _opSkillUp = clientOpcodes.Get(ClientOpcode.UserSkillUpRequest);
        _opSkillUse = clientOpcodes.Get(ClientOpcode.UserSkillUseRequest);
        _opSkillCancel = clientOpcodes.Get(ClientOpcode.UserSkillCancelRequest);
        _opAbilityUp = clientOpcodes.Get(ClientOpcode.UserAbilityUpRequest);
        _opAbilityMassUp = clientOpcodes.Get(ClientOpcode.UserAbilityMassUpRequest);
        _opMobMove = clientOpcodes.Get(ClientOpcode.MobMove);
        _opTransferField = clientOpcodes.Get(ClientOpcode.UserTransferFieldRequest);
        _opSelectNpc = clientOpcodes.Get(ClientOpcode.UserSelectNpc);
        _opScriptAnswer = clientOpcodes.Get(ClientOpcode.UserScriptMessageAnswer);
        _opPortalScript = clientOpcodes.Get(ClientOpcode.UserPortalScriptRequest);
        _opWhisper = clientOpcodes.Get(ClientOpcode.Whisper);
        _opMessenger = clientOpcodes.Get(ClientOpcode.Messenger);
        _opPartyRequest = clientOpcodes.Get(ClientOpcode.PartyRequest);
    }

    /// <summary>The character bound to this session after a successful migrate-in.</summary>
    public Character? Player => _player?.Character;

    public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
    {
        if (opcode == _opMigrateIn)
        {
            await HandleMigrateInAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opUserMove)
        {
            await HandleUserMoveAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opUserChat)
        {
            await HandleUserChatAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opWhisper)
        {
            await HandleWhisperAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMessenger)
        {
            await HandleMessengerAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opPartyRequest)
        {
            await HandlePartyRequestAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opUserEmotion)
        {
            await HandleUserEmotionAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opUserSit)
        {
            await HandleUserSitAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMeleeAttack)
        {
            await HandleMeleeAttackAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMagicAttack)
        {
            await HandleMagicAttackAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opShootAttack)
        {
            await HandleShootAttackAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opUserHit)
        {
            await HandleUserHitAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opDropPickUp)
        {
            await HandleDropPickUpAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opDropMoney)
        {
            await HandleDropMoneyAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opUseItem)
        {
            await HandleUseItemAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opCancelBuff)
        {
            await HandleCancelBuffAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opChangeSlot)
        {
            await HandleChangeSlotPositionAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opGivePopularity)
        {
            await HandleGivePopularityAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opCharacterInfo)
        {
            await HandleCharacterInfoAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opSkillUp)
        {
            await HandleSkillUpAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opSkillUse)
        {
            await HandleSkillUseAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opSkillCancel)
        {
            await HandleSkillCancelAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opAbilityUp)
        {
            await HandleAbilityUpAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opAbilityMassUp)
        {
            await HandleAbilityMassUpAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMobMove)
        {
            await HandleMobMoveAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opTransferField)
        {
            await HandleTransferFieldAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opSelectNpc)
        {
            await HandleSelectNpcAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opShopRequest)
        {
            await HandleShopRequestAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opTrunkRequest)
        {
            await HandleTrunkRequestAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opFuncKeyMapped)
        {
            HandleFuncKeyMapped(packet);
        }
        else if (opcode == _opQuestRequest)
        {
            await HandleQuestRequestAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMiniRoom)
        {
            await HandleMiniRoomAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opFriend)
        {
            await HandleFriendRequestAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opPortalScript)
        {
            await HandlePortalScriptAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opScriptAnswer)
        {
            HandleScriptAnswer(packet);
        }
        else if (opcode == _opAliveAck)
        {
            // Keep-alive acknowledged; nothing to do.
        }
    }

    public override async ValueTask OnDisconnectedAsync(MapleSession session, Exception? error)
    {
        _conversation?.End();
        _conversation = null;

        if (_player is not null && _field is not null)
        {
            _characters.Save(_player.Character); // persist last known map/stats on logout

            // Cancel any open trade so staged items/meso return to their owners.
            if (_trades.Get(_player.Character.Id) is { } trade)
            {
                await CancelTradeAsync(trade).ConfigureAwait(false);
            }

            // Buddies see this player go offline.
            await NotifyBuddiesOfPresenceAsync(_player.Character.Id, channel: -1).ConfigureAwait(false);

            // Buffs don't survive a logout.
            _buffs.Clear(_player.Character.Id);

            // Leave any messenger so the other members' windows drop this player.
            Messenger? messenger = _messengers.GetFor(_player.Character.Id);
            if (messenger is not null)
            {
                await messenger.LeaveAsync(_player.Character.Id).ConfigureAwait(false);
                _messengers.Unregister(_player.Character.Id, messenger);
            }

            // Leave any party (the leader dropping disbands it — a documented simplification).
            Party? party = _parties.GetForCharacter(_player.Character.Id);
            if (party is not null)
            {
                await LeavePartyAsync(party, _player, byDisconnect: true).ConfigureAwait(false);
            }

            _field.Leave(_player.Character.Id);
            await _field.BroadcastAsync(_packets.UserLeaveField(_player.Character.Id)).ConfigureAwait(false);
            await ReleaseControlledMobsAsync(_field, _player.Character.Id).ConfigureAwait(false);
            _player = null;
            _field = null;
        }
    }

    /// <summary>
    /// Releases mobs controlled by a departing player, handing them to another player in the
    /// field when one is present (they receive LP_MobChangeController).
    /// </summary>
    private async ValueTask ReleaseControlledMobsAsync(Field field, int departingCharacterId)
    {
        FieldPlayer? successor = field.Players.FirstOrDefault();

        foreach (FieldMob mob in field.Mobs)
        {
            if (mob.ControllerId != departingCharacterId)
            {
                continue;
            }

            if (successor is null || mob.IsDead)
            {
                mob.ControllerId = -1;
                continue;
            }

            mob.ControllerId = successor.Character.Id;
            try
            {
                await successor.Session.SendAsync(_packets.MobChangeController(mob)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                mob.ControllerId = -1; // successor is going away too
            }
        }
    }

    private async ValueTask HandleMigrateInAsync(MapleSession session, PacketReader packet)
    {
        // JMS v186: [characterId:4][machineId:16][unk:2][unk:1][clientKey:8]
        int characterId = packet.ReadInt();

        Character? character = _characters.Find(characterId);
        if (character is null)
        {
            return;
        }

        var player = new FieldPlayer(character, session);
        _player = player;
        session.UserData = character;

        (int, int, int) seeds = (RandomSeed(), RandomSeed(), RandomSeed());
        await session.SendAsync(_packets.SetFieldEnterGame(character, _channelId, seeds)).ConfigureAwait(false);

        // Post-SetField initialization the JMS v186 client expects to finish entering the field.
        // Byte-for-byte order captured from the reference server's ResCClientSocket.OnMigrateIn:
        // StatChanged -> ForcedStatReset -> pet-consume ×3 -> key map -> macros -> buddy list ->
        // family info -> broadcast slide. (LP_FamilyPrivilegeList, a large version-constant table,
        // is intentionally not sent yet - see ChannelPackets.FamilyInfoResult.)
        await session.SendAsync(_packets.StatChanged(character, (StatFlag)0)).ConfigureAwait(false);
        await session.SendAsync(_packets.ForcedStatReset()).ConfigureAwait(false);
        await session.SendAsync(_packets.PetConsumeItemInit()).ConfigureAwait(false);
        await session.SendAsync(_packets.PetConsumeMpItemInit()).ConfigureAwait(false);
        await session.SendAsync(_packets.PetConsumeCureItemInit()).ConfigureAwait(false);
        await session.SendAsync(_packets.FuncKeyMappedInit(_keymaps.Get(character.Id))).ConfigureAwait(false);
        await session.SendAsync(_packets.MacroSysDataInit()).ConfigureAwait(false);
        await session.SendAsync(BuildBuddyList(character, ChannelPackets.FriendLoadDone)).ConfigureAwait(false);
        await session.SendAsync(_packets.FamilyInfoResult()).ConfigureAwait(false);
        await session.SendAsync(_packets.BroadcastSlideClear()).ConfigureAwait(false);

        // Join the field: tell the newcomer about everyone already there, and vice versa.
        Field field = _fields.Get(character.MapId);
        foreach (FieldPlayer other in field.Players)
        {
            await session.SendAsync(_packets.UserEnterField(other)).ConfigureAwait(false);
        }

        field.Enter(player);
        _field = field;
        await field.BroadcastAsync(_packets.UserEnterField(player), exceptCharacterId: character.Id)
            .ConfigureAwait(false);

        await SpawnNpcsAsync(session, field).ConfigureAwait(false);
        await NotifyBuddiesOfPresenceAsync(character.Id, channel: 0).ConfigureAwait(false); // "came online"

        // Friend requests that arrived while offline pop up now.
        foreach ((int fromId, BuddyEntry entry) in character.Buddies.ToList())
        {
            if (entry.Hidden && _characters.Find(fromId) is { } from)
            {
                await session.SendAsync(_packets.BuddyInvite(fromId, from.Name, from.Level, from.Job)).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask SpawnNpcsAsync(MapleSession session, Field field)
    {
        foreach (FieldNpc npc in field.Npcs)
        {
            await session.SendAsync(_packets.NpcEnterField(npc)).ConfigureAwait(false);
        }

        int characterId = _player!.Character.Id;
        foreach (FieldMob mob in field.Mobs)
        {
            if (mob.IsDead)
            {
                continue;
            }

            await session.SendAsync(_packets.MobEnterField(mob)).ConfigureAwait(false);

            // Uncontrolled mobs are delegated to this client (client-side AI simulation).
            if (mob.ControllerId is -1 || mob.ControllerId == characterId)
            {
                mob.ControllerId = characterId;
                await session.SendAsync(_packets.MobChangeController(mob)).ConfigureAwait(false);
            }
        }

        // Show the newcomer the drops already lying on the ground (no fall animation).
        foreach (FieldDrop drop in field.Drops)
        {
            await session.SendAsync(_packets.DropEnterField(drop, onGround: true)).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleUserMoveAsync(PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_UserMove: fixed prefix then the raw CMovePath buffer, which is relayed
        // verbatim (ResCUserRemote.UserMove re-emits the parsed bytes unchanged).
        if (packet.Remaining <= MovePrefixLength)
        {
            return;
        }

        packet.Skip(MovePrefixLength);
        byte[] movePath = packet.ReadRemaining();

        UpdatePositionFromMovePath(_player, movePath);
        _player.LastActiveTick = Environment.TickCount64; // moving delays HP/MP regen
        _player.Seated = false;                            // and stands you up

        await _field.BroadcastAsync(
            _packets.UserMove(_player.Character.Id, movePath),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
    }

    private async ValueTask HandleMeleeAttackAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        AttackInfo attack = AttackParser.ParseMelee(packet);
        await PrepareSkillAttackAsync(session, attack).ConfigureAwait(false);
        await _field.BroadcastAsync(
            _packets.UserMeleeAttack(_player.Character.Id, _player.Character.Level, attack),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
        await ApplyAttackDamageAsync(session, attack).ConfigureAwait(false);
    }

    private async ValueTask HandleMagicAttackAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        AttackInfo attack = AttackParser.ParseMagic(packet); // v186: same layout as melee
        await PrepareSkillAttackAsync(session, attack).ConfigureAwait(false);
        await _field.BroadcastAsync(
            _packets.UserMagicAttack(_player.Character.Id, _player.Character.Level, attack),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
        await ApplyAttackDamageAsync(session, attack).ConfigureAwait(false);
    }

    private async ValueTask HandleShootAttackAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        AttackInfo attack = AttackParser.ParseShoot(packet);
        await PrepareSkillAttackAsync(session, attack).ConfigureAwait(false);

        // Resolve the bullet (arrow/star/bullet) from the shooter's USE slot so onlookers see the
        // right projectile, and consume one per shot.
        int bulletItemId = 0;
        if (attack.BulletSlot > 0)
        {
            Character c = _player.Character;
            if (Inventory.ItemAt(c, UseTab, attack.BulletSlot) is { } bullet)
            {
                bulletItemId = bullet.ItemId;
                InventoryChange? change = Inventory.RemoveFromSlot(c, UseTab, attack.BulletSlot, 1);
                _characters.Save(c);
                if (change is { } ch)
                {
                    await session.SendAsync(_packets.InventoryOperation(new[] { ch })).ConfigureAwait(false);
                }
            }
        }

        await _field.BroadcastAsync(
            _packets.UserShootAttack(_player.Character.Id, _player.Character.Level, attack, bulletItemId, _player.X, _player.Y),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
        await ApplyAttackDamageAsync(session, attack).ConfigureAwait(false);
    }

    /// <summary>
    /// For a skill-based attack: fills in the caster's learned level (so the field mirror renders
    /// the skill correctly) and deducts the skill's MP cost from wz. Shared by the three attack
    /// handlers; a plain (skill-less) attack is untouched.
    /// </summary>
    private async ValueTask PrepareSkillAttackAsync(MapleSession session, AttackInfo attack)
    {
        if (attack.SkillId <= 0 || _player is null)
        {
            return;
        }

        Character c = _player.Character;
        int level = c.Skills.TryGetValue(attack.SkillId, out int lvl) && lvl > 0 ? lvl : 1;
        attack.SkillLevel = level;

        if (_skills.GetSkillEffect(attack.SkillId, level) is { MpCon: > 0 } effect && c.Mp >= effect.MpCon)
        {
            c.Mp = (short)(c.Mp - effect.MpCon);
            _characters.Save(c);
            await session.SendAsync(_packets.StatChanged(c, StatFlag.Mp)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Applies an attack's per-target damage to the mobs in the field: hurts each live target,
    /// and on death releases control, announces the leave, grants exp, and drops meso. Shared by
    /// the melee / magic / ranged handlers. Damage is currently client-reported (see AGENTS.md).
    /// </summary>
    private async ValueTask ApplyAttackDamageAsync(MapleSession session, AttackInfo attack)
    {
        if (_player is not null)
        {
            _player.LastActiveTick = Environment.TickCount64; // attacking delays HP/MP regen
            _player.Seated = false;                            // and stands you up
        }

        foreach (AttackTarget target in attack.Targets)
        {
            FieldMob? mob = _field!.FindMob(target.MobObjectId);
            if (mob is null || mob.IsDead)
            {
                continue;
            }

            // Server authority: bound the client-reported damage to what a legit pre-BB client
            // can produce (per-line cap) rather than trusting target.TotalDamage verbatim.
            long damage = DamageValidator.ValidatedDamage(target);
            mob.Damage(damage > int.MaxValue ? int.MaxValue : (int)damage);

            // Bosses show an HP gauge to the whole field as they're whittled down.
            if (mob.IsBoss)
            {
                await _field!.BroadcastAsync(_packets.MobHpTag(mob)).ConfigureAwait(false);
            }

            if (mob.IsDead)
            {
                mob.ControllerId = -1;
                mob.RespawnAtTick = MobRespawnService.NextRespawnTick(mob.MobTime); // 0 = never (boss)
                await _field.BroadcastAsync(_packets.MobLeaveField(mob.ObjectId)).ConfigureAwait(false);
                await GrantKillExpAsync(mob.Exp).ConfigureAwait(false);
                await UpdateQuestKillsAsync(session, mob.TemplateId).ConfigureAwait(false);
                await DropLootAsync(mob).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Rolls a killed mob's drop table and spawns the loot on the field (ports
    /// <c>TacosReward.dropFromDatabase</c>): each entry drops on a <c>rand(0..999) &lt; chance</c> test
    /// (bosses drop unconditionally), meso rows become meso piles and item rows become item stacks,
    /// fanned out horizontally. A mob with no drop table falls back to a small meso pile so a kill
    /// still rewards. Equip drops are deferred until the equip item body is client-verified.
    /// </summary>
    private async ValueTask DropLootAsync(FieldMob mob)
    {
        if (_field is null)
        {
            return;
        }

        IReadOnlyList<DropEntry> entries = _dropTable.GetDrops(mob.TemplateId);
        if (entries.Count == 0)
        {
            await DropPlaceholderMesoAsync(mob).ConfigureAwait(false);
            return;
        }

        int dropped = 0;
        foreach (DropEntry entry in entries)
        {
            // Quest-locked drops only fall for a killer who is on that quest (the reference gates
            // them by quest status; per-viewer visibility is simplified to the killer's status).
            if (entry.QuestId > 0 && _player?.Character.StartedQuests.ContainsKey(entry.QuestId) != true)
            {
                continue;
            }

            if (!DropRoller.ShouldDrop(entry, Random.Shared.Next(1000), forced: mob.IsBoss, rate: _rates.Drop))
            {
                continue;
            }

            short x = (short)(mob.X + DropRoller.ScatterX(dropped));
            if (entry.ItemId == 0)
            {
                int meso = (int)(DropRoller.MesoAmount(entry, Random.Shared.Next) * _rates.Meso);
                if (meso <= 0)
                {
                    continue;
                }

                FieldDrop drop = _field.AddMesoDrop(meso, x, mob.Y, mob);
                await _field.BroadcastAsync(_packets.DropEnterFieldMeso(drop)).ConfigureAwait(false);
            }
            else
            {
                int qty = DropRoller.ItemQuantity(entry, Random.Shared.Next);
                FieldDrop drop = _field.AddItemDrop(entry.ItemId, (short)Math.Clamp(qty, 1, short.MaxValue), x, mob.Y, mob);
                await _field.BroadcastAsync(_packets.DropEnterFieldItem(drop)).ConfigureAwait(false);
            }

            dropped++;
        }
    }

    /// <summary>Drops a small meso pile for a mob with no drop table (so kills still reward).</summary>
    private async ValueTask DropPlaceholderMesoAsync(FieldMob mob)
    {
        if (_field is null)
        {
            return;
        }

        int meso = Math.Max(1, (int)(mob.MaxHp / 5 * _rates.Meso)); // placeholder formula
        FieldDrop drop = _field.AddMesoDrop(meso, mob.X, mob.Y, mob);
        await _field.BroadcastAsync(_packets.DropEnterFieldMeso(drop)).ConfigureAwait(false);
    }

    /// <summary>
    /// Byte length of the JMS v186 CP_MobMove fields between the skill int and the CMovePath:
    /// int ×2 (JMS &gt;= 186), byte, int, 0xFFDDCC ×2, int.
    /// </summary>
    private const int MobMoveMidLength = 4 + 4 + 1 + 4 + 4 + 4 + 4;

    private async ValueTask HandleMobMoveAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_MobMove:
        //   [mobOid:4][moveId:2][nextAttack:1][left:1][mobSkill:4][mid fields][movePath raw]
        int mobOid = packet.ReadInt();
        short moveId = packet.ReadShort();
        bool nextAttackPossible = packet.ReadBool();
        byte left = packet.ReadByte();
        int mobSkill = packet.ReadInt();

        if (packet.Remaining <= MobMoveMidLength)
        {
            return;
        }

        packet.Skip(MobMoveMidLength);
        byte[] movePath = packet.ReadRemaining();

        FieldMob? mob = _field.FindMob(mobOid);
        if (mob is null || mob.IsDead)
        {
            return;
        }

        // Only the assigned controller may steer the mob; adopt it if it has none.
        int characterId = _player.Character.Id;
        if (mob.ControllerId is -1)
        {
            mob.ControllerId = characterId;
        }
        else if (mob.ControllerId != characterId)
        {
            return;
        }

        // Track the path origin as the mob's position (same convention as player movement).
        if (movePath.Length >= 4)
        {
            mob.X = (short)(movePath[0] | (movePath[1] << 8));
            mob.Y = (short)(movePath[2] | (movePath[3] << 8));
        }

        await session.SendAsync(_packets.MobCtrlAck(mob, moveId, aggro: false)).ConfigureAwait(false);
        await _field.BroadcastAsync(
            _packets.MobMove(mob.ObjectId, nextAttackPossible, left, mobSkill, movePath),
            exceptCharacterId: characterId).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserHit</c> — the client reports the damage its player took from a mob.
    /// Applies the HP loss and pushes <c>LP_StatChanged</c>. HP is floored at 1 for now (death /
    /// revive is a follow-up). Damage is client-reported, the MapleStory norm (see AGENTS.md).
    /// </summary>
    private async ValueTask HandleUserHitAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        // JMS v186 CP_UserHit prefix: [time:4][nAttackIdx:1][nMagicElemAttr:1][nDamage:4] ...
        packet.ReadInt();   // time
        packet.ReadByte();  // nAttackIdx
        packet.ReadByte();  // nMagicElemAttr
        int damage = DamageValidator.ClampLine(packet.ReadInt()); // bound the client-reported hit
        if (damage <= 0)
        {
            return; // a miss / no damage
        }

        Character c = _player.Character;
        _player.LastActiveTick = Environment.TickCount64; // taking a hit counts as activity
        c.Hp = (short)Math.Max(0, c.Hp - damage);          // 0 HP = dead (client shows the tombstone)

        StatFlag changed = StatFlag.Hp;
        if (c.Hp == 0)
        {
            changed |= CharacterProgression.ApplyDeathPenalty(c); // dying costs some exp
        }

        await session.SendAsync(_packets.StatChanged(c, changed)).ConfigureAwait(false);
        await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false); // party sees the health drop
    }

    /// <summary>
    /// Handles <c>CP_UserAbilityUpRequest</c> — spends one ability point on a base stat (ports
    /// <c>ReqCUser.OnUserAbilityUpRequest</c>). The flag is a <c>CS_*</c> bit that maps 1:1 onto
    /// <see cref="StatFlag"/>. Rejected requests (no AP / capped) send nothing, matching the client
    /// which only updates from the resulting <c>LP_StatChanged</c>.
    /// </summary>
    private async ValueTask HandleAbilityUpAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        packet.ReadInt();                          // timestamp
        var stat = (StatFlag)packet.ReadInt();     // CS_* flag == StatFlag bit

        StatFlag changed = CharacterProgression.SpendAbilityPoint(_player.Character, stat);
        if (changed == 0)
        {
            return; // no AP, capped stat, or a non-assignable flag
        }

        _characters.Save(_player.Character);
        await session.SendAsync(_packets.StatChanged(_player.Character, changed)).ConfigureAwait(false);
    }

    /// <summary>Upper bound on mass-up allocations (the client sends 2; guards a malformed count).</summary>
    private const int MaxAbilityAllocations = 8;

    /// <summary>
    /// Handles <c>CP_UserAbilityMassUpRequest</c> — the auto-assign button that spends all AP across
    /// several base stats at once (ports <c>OnUserAbilityMassUpRequest</c>). Reads the
    /// <c>[stat:4][points:4]</c> pairs and applies them via
    /// <see cref="CharacterProgression.SpendAllAbilityPoints"/>; an invalid batch is ignored.
    /// </summary>
    private async ValueTask HandleAbilityMassUpAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        packet.ReadInt();                 // timestamp
        int count = packet.ReadInt();
        if (count < 1 || count > MaxAbilityAllocations)
        {
            return;
        }

        var allocations = new List<(StatFlag, int)>(count);
        for (int i = 0; i < count; i++)
        {
            var stat = (StatFlag)packet.ReadInt();
            int points = packet.ReadInt();
            allocations.Add((stat, points));
        }

        StatFlag changed = CharacterProgression.SpendAllAbilityPoints(_player.Character, allocations);
        if (changed == 0)
        {
            return;
        }

        _characters.Save(_player.Character);
        await session.SendAsync(_packets.StatChanged(_player.Character, changed)).ConfigureAwait(false);
    }

    private async ValueTask HandleSkillUpAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        // JMS v186 CP_UserSkillUpRequest: [timeStamp:4][skillId:4]
        packet.ReadInt();
        int skillId = packet.ReadInt();

        Character c = _player.Character;
        if (c.Sp <= 0)
        {
            return; // no SP to spend
        }

        c.Skills.TryGetValue(skillId, out int current);

        // Cap at the skill's wz max level (when known) so SP can't over-level a skill.
        int maxLevel = _skills.GetMaxLevel(skillId);
        if (maxLevel > 0 && current >= maxLevel)
        {
            return; // already maxed
        }

        int level = current + 1;
        c.Skills[skillId] = level;
        c.Sp = (short)Math.Max(0, c.Sp - 1);
        _characters.Save(c);

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Sp)).ConfigureAwait(false);
        await session.SendAsync(_packets.ChangeSkillRecordResult(skillId, level)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserSkillUseRequest</c> — casting a self-buff skill (ports
    /// <c>ReqCUser.OnUserSkillUseRequest</c> + <c>TacosBuff.update</c>): acks with
    /// <c>LP_SkillUseResult</c>, deducts the skill's MP cost, and applies its temporary stat buff via
    /// <c>LP_TemporaryStatSet</c> (reason = the positive skill id, duration from wz). Attack skills go
    /// through the attack handlers, not here. The client only offers skills the player owns, so skill
    /// ownership isn't re-validated server-side yet.
    /// </summary>
    private async ValueTask HandleSkillUseAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        // JMS v186 CP_UserSkillUseRequest (self-buff): [updateTime:4][skillId:4][skillLevel:1]
        packet.ReadInt();
        int skillId = packet.ReadInt();
        packet.ReadByte(); // client-claimed level — the server uses the learned level instead

        // The reference acks every cast unconditionally.
        await session.SendAsync(_packets.SkillUseResult()).ConfigureAwait(false);

        Character c = _player.Character;
        int level = c.Skills.TryGetValue(skillId, out int learned) ? learned : 0;
        if (level <= 0)
        {
            return; // skill not learned — server authority over the cast
        }

        SkillEffect? effect = _skills.GetSkillEffect(skillId, level);
        if (effect is null)
        {
            return; // unknown skill / no wz effect
        }
        if (effect.MpCon > 0 && c.Mp >= effect.MpCon)
        {
            c.Mp = (short)(c.Mp - effect.MpCon);
            _characters.Save(c);
            await session.SendAsync(_packets.StatChanged(c, StatFlag.Mp)).ConfigureAwait(false);
        }

        List<BuffStat> buffs = SkillBuff.FromEffect(skillId, effect);
        if (buffs.Count == 0)
        {
            return;
        }

        byte[] buffPacket = _packets.TemporaryStatSet(buffs);
        uint mask = BuffEffect.Word0Mask(buffs);
        await session.SendAsync(buffPacket).ConfigureAwait(false);
        _buffs.Register(c.Id, skillId, mask, effect.DurationMs);

        // A party buff (Haste, Rage, Hyper Body, … — marked by the wz affect box) also lands on
        // party members in the same map (ports the isPartyBuff apply; range box simplified to map).
        if (effect.HasPartyArea && _parties.GetForCharacter(c.Id) is { } party)
        {
            foreach (FieldPlayer member in party.Members)
            {
                if (member.Character.Id != c.Id && member.Character.MapId == c.MapId)
                {
                    await TrySendAsync(member, buffPacket).ConfigureAwait(false);
                    _buffs.Register(member.Character.Id, skillId, mask, effect.DurationMs);
                }
            }
        }
    }

    /// <summary>
    /// Handles <c>CP_UserSkillCancelRequest</c> — the player ends a skill buff early (ports
    /// <c>OnUserSkillCancelRequest</c>): clears that skill's temporary-stat mask with
    /// <c>LP_TemporaryStatReset</c> (recomputed from the skill's wz effect at the player's level).
    /// </summary>
    private async ValueTask HandleSkillCancelAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        int skillId = packet.ReadInt();
        Character c = _player.Character;
        int level = c.Skills.TryGetValue(skillId, out int lvl) ? lvl : 1;
        if (_skills.GetSkillEffect(skillId, level) is not { } effect)
        {
            return;
        }

        uint mask = BuffEffect.Word0Mask(SkillBuff.FromEffect(skillId, effect));
        if (mask != 0)
        {
            _buffs.Remove(c.Id, skillId);
            await session.SendAsync(_packets.TemporaryStatReset(mask)).ConfigureAwait(false);
        }
    }

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

        FieldDrop? drop = _field.RemoveDrop(dropOid);
        if (drop is null)
        {
            return; // already taken
        }

        await _field.BroadcastAsync(_packets.DropLeaveFieldPickup(dropOid, _player.Character.Id))
            .ConfigureAwait(false);

        Character c = _player.Character;
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
    private const int MinMesoDrop = 10;
    private const int MaxMesoDrop = 50000;

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

        packet.ReadInt();                 // timestamp
        short slot = packet.ReadShort();
        int itemId = packet.ReadInt();

        Character c = _player.Character;
        InventoryItem? item = Inventory.ItemAt(c, UseTab, slot);
        if (item is null || item.ItemId != itemId || item.Quantity < 1)
        {
            return; // desync / already gone
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
            List<BuffStat> buffs = BuffEffect.FromSpec(spec);
            if (buffs.Count > 0)
            {
                await session.SendAsync(_packets.TemporaryStatSet(buffs)).ConfigureAwait(false);
                _buffs.Register(c.Id, -spec.ItemId, BuffEffect.Word0Mask(buffs), spec.Time);
            }
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
        if (itemId <= 0 || _items.GetConsume(itemId) is not { } spec)
        {
            return;
        }

        uint mask = BuffEffect.Word0Mask(BuffEffect.FromSpec(spec));
        if (mask != 0)
        {
            _buffs.Remove(_player.Character.Id, buffId);
            await session.SendAsync(_packets.TemporaryStatReset(mask)).ConfigureAwait(false);
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

        // Equipped→equipped moves aren't allowed; ignore rather than desync.
        if (tab == equipTab && src < 0 && dst < 0)
        {
            return;
        }

        Character c = _player.Character;
        if (Inventory.Move(c, tab, src, dst) is not { } change)
        {
            return; // empty source slot / no-op
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

        // Token-currency (second-currency) shops aren't modelled yet.
        if (entry.ReqItem > 0)
        {
            await session.SendAsync(_packets.ShopResult(ShopResultCode.BuyUnknown)).ConfigureAwait(false);
            return;
        }

        Character c = _player!.Character;
        long price = (long)entry.Price * quantity;
        if (entry.Price < 0 || c.Meso < price)
        {
            await session.SendAsync(_packets.ShopResult(ShopResultCode.BuyNoMoney)).ConfigureAwait(false);
            return;
        }

        c.Meso -= (int)price;
        int slotMax = _items.GetConsume(itemId)?.SlotMax ?? Inventory.DefaultSlotMax;
        List<InventoryChange> changes = Inventory.Add(c, itemId, quantity, slotMax);
        PopulateEquipStats(changes); // a bought equip gets its wz base stats
        _characters.Save(c);

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
            return; // already full — the client shouldn't ask
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
    private const byte QuestReqAccept = 1;
    private const byte QuestReqComplete = 2;
    private const byte QuestReqResign = 3;
    private const byte QuestReqOpeningScript = 4;
    private const byte QuestReqCompleteScript = 5;

    /// <summary>
    /// Handles <c>CP_UserQuestRequest</c> — accepting / completing / forfeiting a quest through the
    /// client's quest dialog (ports <c>ReqCUser.OnUserQuestRequest</c> + <c>MapleQuest</c>).
    /// Accept gates on the start check's level, seeds the mob-kill progress, and applies the start
    /// acts; complete verifies the end check (kills + items), applies the rewards (exp / meso /
    /// fame / items, negative counts taken away), and plays the completion effect. Script-driven
    /// quests run <c>scripts/quest/{questId}.js</c> (<c>start()</c> / <c>end()</c> with the global
    /// <c>qm</c>); lost-item recovery isn't modelled yet.
    /// </summary>
    private async ValueTask HandleQuestRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        byte action = packet.ReadByte();
        int questId = packet.ReadShort() & 0xFFFF;
        Character c = _player.Character;

        switch (action)
        {
            case QuestReqAccept:
            {
                int npcId = packet.ReadInt();
                await AcceptQuestAsync(session, c, questId, npcId).ConfigureAwait(false);
                break;
            }

            case QuestReqOpeningScript: // scripts/quest/{questId}.js start(); plain accept if none
            {
                int npcId = packet.ReadInt();
                if (!TryStartQuestScript(session, questId, npcId, ending: false))
                {
                    await AcceptQuestAsync(session, c, questId, npcId).ConfigureAwait(false);
                }

                break;
            }

            case QuestReqComplete:
            {
                int npcId = packet.ReadInt();
                int selection = packet.Remaining >= 4 ? packet.ReadInt() : -1;
                await CompleteQuestAsync(session, c, questId, npcId, selection).ConfigureAwait(false);
                break;
            }

            case QuestReqCompleteScript: // scripts/quest/{questId}.js end(); plain complete if none
            {
                int npcId = packet.ReadInt();
                if (!TryStartQuestScript(session, questId, npcId, ending: true))
                {
                    await CompleteQuestAsync(session, c, questId, npcId).ConfigureAwait(false);
                }

                break;
            }

            case QuestReqResign:
                if (c.StartedQuests.Remove(questId))
                {
                    _characters.Save(c);
                    await session.SendAsync(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordNone)).ConfigureAwait(false);
                }

                break;
        }
    }

    /// <summary>
    /// Runs a quest's script (ports <c>TacosScriptQuest.startQuest/endQuest</c>): the script drives
    /// the dialog through <c>qm</c> and grants/verifies through <c>player</c>. False when the quest
    /// has no script (caller falls back to the data-driven path) or a conversation is already open.
    /// </summary>
    private bool TryStartQuestScript(MapleSession session, int questId, int npcId, bool ending)
    {
        if (_npcScripts is null || _conversation is { IsEnded: false })
        {
            return false;
        }

        var dialog = new ChannelNpcDialog(session, _packets);
        NpcConversation? conversation = _npcScripts.StartQuest(questId, npcId, dialog, CreateScriptPlayer(session), ending);
        if (conversation is null)
        {
            return false;
        }

        _conversation = conversation;
        return true;
    }

    private async ValueTask AcceptQuestAsync(MapleSession session, Character c, int questId, int npcId)
    {
        if (c.StartedQuests.ContainsKey(questId) || c.CompletedQuests.ContainsKey(questId))
        {
            return;
        }

        QuestData? quest = _quests.GetQuest(questId);
        if (quest?.StartCheck is { } start)
        {
            if (start.LevelMin > 0 && c.Level < start.LevelMin)
            {
                return; // under-leveled
            }

            foreach (QuestPrereq prereq in start.Quests)
            {
                bool met = prereq.State == 1
                    ? c.StartedQuests.ContainsKey(prereq.QuestId)
                    : c.CompletedQuests.ContainsKey(prereq.QuestId);
                if (!met)
                {
                    return; // prerequisite quest not at the required state
                }
            }

            if (start.Jobs.Count > 0 && !start.Jobs.Contains(c.Job))
            {
                return; // wrong job
            }
        }

        string progress = InitialQuestProgress(quest);
        c.StartedQuests[questId] = progress;

        if (quest?.StartAct is { } act)
        {
            await ApplyQuestActAsync(session, c, act).ConfigureAwait(false);
        }

        _characters.Save(c);
        await session.SendAsync(_packets.UserQuestResult(questId, npcId)).ConfigureAwait(false);
        await session.SendAsync(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordStarted, progress)).ConfigureAwait(false);
    }

    private async ValueTask CompleteQuestAsync(MapleSession session, Character c, int questId, int npcId, int selection = -1)
    {
        if (!c.StartedQuests.ContainsKey(questId))
        {
            return;
        }

        QuestData? quest = _quests.GetQuest(questId);
        if (quest is null || !QuestRequirementsMet(c, quest))
        {
            return; // unknown quest or unmet kills/items — the dialog stays open
        }

        if (quest.EndAct is { } act)
        {
            await ApplyQuestActAsync(session, c, act, selection).ConfigureAwait(false);
        }

        c.StartedQuests.Remove(questId);
        c.CompletedQuests[questId] = CharacterDataEncoder.FileTimeNow();
        _characters.Save(c);

        await session.SendAsync(_packets.UserQuestResult(questId, npcId)).ConfigureAwait(false);
        await session.SendAsync(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordCompleted)).ConfigureAwait(false);
        await session.SendAsync(_packets.UserEffectLocal(ChannelPackets.UserEffectQuestComplete)).ConfigureAwait(false);
        if (_field is not null)
        {
            await _field.BroadcastAsync(
                _packets.UserEffectRemote(c.Id, ChannelPackets.UserEffectQuestComplete),
                exceptCharacterId: c.Id).ConfigureAwait(false);
        }
    }

    /// <summary>Zeroed per-mob progress ("000" per required mob) for a fresh quest record.</summary>
    private static string InitialQuestProgress(QuestData? quest)
        => quest?.EndCheck is { Mobs.Count: > 0 } end
            ? string.Concat(Enumerable.Repeat("000", end.Mobs.Count))
            : string.Empty;

    /// <summary>All end-check kills reached and required items held.</summary>
    private bool QuestRequirementsMet(Character c, QuestData quest)
    {
        if (quest.EndCheck is not { } check)
        {
            return true;
        }

        if (check.Mobs.Count > 0)
        {
            string progress = c.StartedQuests.TryGetValue(quest.QuestId, out string? p) ? p : string.Empty;
            for (int i = 0; i < check.Mobs.Count; i++)
            {
                if (QuestProgressCount(progress, i) < check.Mobs[i].Count)
                {
                    return false;
                }
            }
        }

        foreach (QuestItemEntry req in check.Items)
        {
            if (req.Count > 0 && CountInventoryItem(c, req.ItemId) < req.Count)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Applies a quest act: give/take items, meso, fame, and exp. Selectable rewards (prop == -1)
    /// give only the row the player picked (<paramref name="selection"/> indexes the selectable
    /// rows in wz order); weighted-lottery rows (prop &gt; 0) aren't modelled yet and are skipped.
    /// </summary>
    private async ValueTask ApplyQuestActAsync(MapleSession session, Character c, QuestAct act, int selection = -1)
    {
        var changes = new List<InventoryChange>();
        QuestItemEntry? lotteryPick = PickLotteryReward(act.Items);
        int selectableIndex = 0;
        foreach (QuestItemEntry item in act.Items)
        {
            if (item.Prop is -1)
            {
                if (selectableIndex++ != selection)
                {
                    continue; // not the row the player chose
                }
            }
            else if (item.Prop is > 0)
            {
                if (!ReferenceEquals(item, lotteryPick))
                {
                    continue; // lottery: only the weighted-random winner is given
                }
            }
            else if (item.Prop is not null)
            {
                continue; // prop 0: unused marker rows
            }

            if (item.Count > 0)
            {
                int slotMax = _items.GetConsume(item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax;
                changes.AddRange(Inventory.Add(c, item.ItemId, item.Count, slotMax));
            }
            else if (item.Count < 0)
            {
                changes.AddRange(RemoveInventoryQuantity(c, item.ItemId, -item.Count));
            }
        }

        PopulateEquipStats(changes);
        if (changes.Count > 0)
        {
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        StatFlag flags = 0;
        if (act.Money != 0)
        {
            c.Meso = (int)Math.Clamp((long)c.Meso + act.Money, 0, int.MaxValue);
            flags |= StatFlag.Meso;
        }

        if (act.Fame != 0)
        {
            c.Fame = (short)Math.Clamp(c.Fame + act.Fame, -30000, 30000);
            flags |= StatFlag.Fame;
        }

        if (flags != 0)
        {
            await session.SendAsync(_packets.StatChanged(c, flags)).ConfigureAwait(false);
        }

        if (act.Money > 0)
        {
            await session.SendAsync(_packets.IncMoneyMessage(act.Money)).ConfigureAwait(false);
        }

        if (act.Exp > 0 && _player is not null)
        {
            await GrantExpToAsync(_player, act.Exp).ConfigureAwait(false);
        }
    }

    /// <summary>Picks the weighted-random winner among lottery (<c>prop &gt; 0</c>) reward rows.</summary>
    private static QuestItemEntry? PickLotteryReward(IReadOnlyList<QuestItemEntry> items)
    {
        int total = 0;
        foreach (QuestItemEntry item in items)
        {
            if (item.Prop is > 0)
            {
                total += item.Prop.Value;
            }
        }

        if (total <= 0)
        {
            return null;
        }

        int roll = Random.Shared.Next(total);
        foreach (QuestItemEntry item in items)
        {
            if (item.Prop is > 0)
            {
                roll -= item.Prop.Value;
                if (roll < 0)
                {
                    return item;
                }
            }
        }

        return null;
    }

    /// <summary>Removes a total quantity of an item across its inventory slots.</summary>
    private static List<InventoryChange> RemoveInventoryQuantity(Character c, int itemId, int quantity)
    {
        var changes = new List<InventoryChange>();
        int tab = Inventory.Tab(itemId);
        int remaining = quantity;
        foreach (InventoryItem item in c.EquippedItems
                     .Where(i => i.ItemId == itemId && i.Position > 0)
                     .OrderBy(i => i.Position)
                     .ToList())
        {
            if (remaining <= 0)
            {
                break;
            }

            int take = Math.Min(remaining, item.Quantity);
            if (Inventory.RemoveFromSlot(c, tab, item.Position, take) is { } change)
            {
                changes.Add(change);
            }

            remaining -= take;
        }

        return changes;
    }

    private static int CountInventoryItem(Character c, int itemId)
        => c.EquippedItems.Where(i => i.ItemId == itemId && i.Position > 0).Sum(i => i.Quantity);

    /// <summary>The 3-digit kill count at a mob index of a quest progress string.</summary>
    private static int QuestProgressCount(string progress, int index)
    {
        int start = index * 3;
        return start + 3 <= progress.Length && int.TryParse(progress.AsSpan(start, 3), out int n) ? n : 0;
    }

    /// <summary>Rebuilds a progress string with one mob's count changed (3 digits per mob).</summary>
    private static string SetQuestProgressCount(string progress, int mobCount, int index, int value)
    {
        char[] buffer = new char[mobCount * 3];
        for (int i = 0; i < mobCount; i++)
        {
            int v = i == index ? value : QuestProgressCount(progress, i);
            Math.Clamp(v, 0, 999).ToString("000").CopyTo(0, buffer, i * 3, 3);
        }

        return new string(buffer);
    }

    /// <summary>
    /// Advances the killer's in-progress kill quests for a slain mob and pushes the journal update
    /// (ports <c>MapleQuestStatus.mobKilled</c> + <c>ResWrapper.updateQuestMobKills</c>: per-mob
    /// 3-digit counts in the quest's Check order).
    /// </summary>
    private async ValueTask UpdateQuestKillsAsync(MapleSession session, int mobTemplateId)
    {
        if (_player is null || _player.Character.StartedQuests.Count == 0)
        {
            return;
        }

        Character c = _player.Character;
        List<(int QuestId, string Progress)>? updates = null;
        foreach (KeyValuePair<int, string> entry in c.StartedQuests.ToList())
        {
            if (_quests.GetQuest(entry.Key)?.EndCheck is not { Mobs.Count: > 0 } check)
            {
                continue;
            }

            int index = -1;
            for (int i = 0; i < check.Mobs.Count; i++)
            {
                if (check.Mobs[i].MobId == mobTemplateId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                continue;
            }

            int current = QuestProgressCount(entry.Value, index);
            if (current >= check.Mobs[index].Count)
            {
                continue; // this mob's requirement is already met
            }

            string updated = SetQuestProgressCount(entry.Value, check.Mobs.Count, index, current + 1);
            c.StartedQuests[entry.Key] = updated;
            (updates ??= new List<(int, string)>()).Add((entry.Key, updated));
        }

        if (updates is null)
        {
            return;
        }

        _characters.Save(c);
        foreach ((int questId, string progress) in updates)
        {
            await session.SendAsync(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordStarted, progress)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_MiniRoom</c> for the trade room (ports <c>ReqCMiniRoomBaseDlg</c> +
    /// <c>MapleTrade</c>): create (type 3) → invite → enter, staging items/meso (removed from the
    /// owner immediately, returned on cancel), and the exchange once both sides press Trade. Other
    /// mini-room types (minigames, personal/hired shops) aren't modelled.
    /// </summary>
    private async ValueTask HandleMiniRoomAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        byte protocol = packet.ReadByte();
        switch (protocol)
        {
            case ChannelPackets.MiniRoomCreate:
            {
                byte roomType = packet.ReadByte();
                if (roomType != 3 || _trades.Get(c.Id) is not null)
                {
                    return; // only trade rooms; one trade at a time
                }

                var trade = new Trade(_player);
                _trades.TryAdd(c.Id, trade);
                await session.SendAsync(_packets.TradeStart(c, 0, null)).ConfigureAwait(false);
                break;
            }

            case ChannelPackets.MiniRoomInvite:
            {
                int targetId = packet.ReadInt();
                Trade? trade = _trades.Get(c.Id);
                if (trade is null || trade.Starter.Player.Character.Id != c.Id || trade.Visitor is not null)
                {
                    return;
                }

                FieldPlayer? target = _field?.Players.FirstOrDefault(p => p.Character.Id == targetId);
                if (target is null || _trades.Get(targetId) is not null)
                {
                    await ReplyAsync(session, "the other player can't trade right now").ConfigureAwait(false);
                    await CancelTradeAsync(trade).ConfigureAwait(false);
                    return;
                }

                trade.InvitedCharacterId = targetId;
                _trades.TryAdd(targetId, trade);
                await TrySendAsync(target, _packets.TradeInvite(c.Name)).ConfigureAwait(false);
                break;
            }

            case ChannelPackets.MiniRoomInviteResult: // the invitee declined
            {
                if (_trades.Get(c.Id) is { } trade)
                {
                    await CancelTradeAsync(trade).ConfigureAwait(false);
                }

                break;
            }

            case ChannelPackets.MiniRoomEnter:
            {
                Trade? trade = _trades.Get(c.Id);
                if (trade is null || trade.VisitorEntered || trade.InvitedCharacterId != c.Id)
                {
                    return;
                }

                trade.Join(_player);
                trade.VisitorEntered = true;
                await session.SendAsync(_packets.TradeStart(c, 1, trade.Starter.Player.Character)).ConfigureAwait(false);
                await TrySendAsync(trade.Starter.Player, _packets.TradePartnerAdd(c)).ConfigureAwait(false);
                break;
            }

            case ChannelPackets.MiniRoomChat:
            {
                packet.ReadInt(); // update time
                string message = packet.ReadString();
                if (_trades.Get(c.Id) is { } trade && trade.SideOf(c.Id) is { } side)
                {
                    byte[] chat = _packets.TradeChat(side.Slot, $"{c.Name} : {message}");
                    await session.SendAsync(chat).ConfigureAwait(false);
                    if (trade.PartnerOf(side) is { } partner)
                    {
                        await TrySendAsync(partner.Player, chat).ConfigureAwait(false);
                    }
                }

                break;
            }

            case ChannelPackets.MiniRoomLeave:
            {
                if (_trades.Get(c.Id) is { } trade)
                {
                    await CancelTradeAsync(trade).ConfigureAwait(false);
                }

                break;
            }

            case ChannelPackets.TradePutItem:
                await HandleTradePutItemAsync(session, c, packet).ConfigureAwait(false);
                break;

            case ChannelPackets.TradePutMoney:
                await HandleTradePutMoneyAsync(session, c, packet).ConfigureAwait(false);
                break;

            case ChannelPackets.TradeConfirm:
                await HandleTradeConfirmAsync(session, c).ConfigureAwait(false);
                break;
        }
    }

    private async ValueTask HandleTradePutItemAsync(MapleSession session, Character c, PacketReader packet)
    {
        // TRP_PutItem: [invType:1][slot:2][qty:2][targetSlot:1]
        int tab = packet.ReadByte();
        short slot = packet.ReadShort();
        int qty = packet.ReadShort();
        byte targetSlot = packet.ReadByte();

        Trade? trade = _trades.Get(c.Id);
        TradeSide? side = trade?.SideOf(c.Id);
        if (trade is null || side is null || side.Locked || !trade.VisitorEntered)
        {
            return;
        }

        InventoryItem? item = Inventory.ItemAt(c, tab, slot);
        if (item is null || qty < 0)
        {
            return;
        }

        InventoryItem staged;
        InventoryChange invChange;
        if (tab == 1 || qty == 0 || qty >= item.Quantity)
        {
            // Move the whole item object (equips keep their stats through the trade).
            c.EquippedItems.Remove(item);
            staged = item;
            invChange = new InventoryChange(InvMode.Remove, tab, slot, null, 0);
        }
        else
        {
            item.Quantity -= (short)qty;
            staged = new InventoryItem { ItemId = item.ItemId, Quantity = (short)qty };
            invChange = new InventoryChange(InvMode.Update, tab, slot, item, item.Quantity);
        }

        staged.Position = targetSlot; // the trade-window slot
        side.Items.Add(staged);
        _characters.Save(c);

        await session.SendAsync(_packets.InventoryOperation(new[] { invChange })).ConfigureAwait(false);
        await session.SendAsync(_packets.TradeItemAdd(0, staged)).ConfigureAwait(false);
        if (trade.PartnerOf(side) is { } partner)
        {
            await TrySendAsync(partner.Player, _packets.TradeItemAdd(1, staged)).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleTradePutMoneyAsync(MapleSession session, Character c, PacketReader packet)
    {
        int meso = packet.ReadInt();
        Trade? trade = _trades.Get(c.Id);
        TradeSide? side = trade?.SideOf(c.Id);
        if (trade is null || side is null || side.Locked || !trade.VisitorEntered || meso <= 0 || c.Meso < meso)
        {
            return;
        }

        c.Meso -= meso;
        side.Meso += meso;
        _characters.Save(c);

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await session.SendAsync(_packets.TradeMesoSet(0, side.Meso)).ConfigureAwait(false);
        if (trade.PartnerOf(side) is { } partner)
        {
            await TrySendAsync(partner.Player, _packets.TradeMesoSet(1, side.Meso)).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleTradeConfirmAsync(MapleSession session, Character c)
    {
        Trade? trade = _trades.Get(c.Id);
        TradeSide? side = trade?.SideOf(c.Id);
        if (trade is null || side is null || side.Locked || !trade.VisitorEntered)
        {
            return;
        }

        side.Locked = true;
        if (trade.PartnerOf(side) is { } partner)
        {
            await TrySendAsync(partner.Player, _packets.TradeConfirmation()).ConfigureAwait(false);
        }

        if (trade.BothLocked)
        {
            await CompleteTradeAsync(trade).ConfigureAwait(false);
        }
    }

    /// <summary>Executes a locked trade: each side receives the other's staged items and meso.</summary>
    private async ValueTask CompleteTradeAsync(Trade trade)
    {
        if (!trade.TryClose())
        {
            return; // the other session's confirm/cancel got here first
        }

        _trades.Remove(trade);
        TradeSide[] sides = { trade.Starter, trade.Visitor! };
        foreach (TradeSide side in sides)
        {
            TradeSide giver = side == trade.Starter ? trade.Visitor! : trade.Starter;
            Character receiver = side.Player.Character;

            var changes = new List<InventoryChange>();
            foreach (InventoryItem item in giver.Items)
            {
                changes.Add(Inventory.Place(receiver, item));
            }

            if (giver.Meso > 0)
            {
                receiver.Meso = (int)Math.Clamp((long)receiver.Meso + giver.Meso, 0, int.MaxValue);
            }

            _characters.Save(receiver);
            if (changes.Count > 0)
            {
                await TrySendAsync(side.Player, _packets.InventoryOperation(changes)).ConfigureAwait(false);
            }

            if (giver.Meso > 0)
            {
                await TrySendAsync(side.Player, _packets.StatChanged(receiver, StatFlag.Meso)).ConfigureAwait(false);
            }

            await TrySendAsync(side.Player, _packets.TradeLeave(side.Slot, ChannelPackets.TradeMsgSuccess)).ConfigureAwait(false);
        }
    }

    /// <summary>Cancels a trade: staged items and meso return to their owners; both sides close.</summary>
    private async ValueTask CancelTradeAsync(Trade trade)
    {
        if (!trade.TryClose())
        {
            return; // already completed/cancelled by the other session
        }

        _trades.Remove(trade);
        foreach (TradeSide? side in new[] { trade.Starter, trade.Visitor })
        {
            if (side is null)
            {
                continue;
            }

            Character owner = side.Player.Character;
            var changes = new List<InventoryChange>();
            foreach (InventoryItem item in side.Items)
            {
                changes.Add(Inventory.Place(owner, item));
            }

            if (side.Meso > 0)
            {
                owner.Meso = (int)Math.Clamp((long)owner.Meso + side.Meso, 0, int.MaxValue);
            }

            _characters.Save(owner);
            if (changes.Count > 0)
            {
                await TrySendAsync(side.Player, _packets.InventoryOperation(changes)).ConfigureAwait(false);
            }

            if (side.Meso > 0)
            {
                await TrySendAsync(side.Player, _packets.StatChanged(owner, StatFlag.Meso)).ConfigureAwait(false);
            }

            await TrySendAsync(side.Player, _packets.TradeLeave(side.Slot, ChannelPackets.TradeMsgCancelled)).ConfigureAwait(false);
        }
    }

    // CP_FriendRequest flags (OpsFriend).
    private const byte FriendReqLoad = 0;
    private const byte FriendReqSet = 1;
    private const byte FriendReqAccept = 2;
    private const byte FriendReqDelete = 3;

    /// <summary>Max buddy-list size (the pre-BB default capacity).</summary>
    private const int BuddyCapacity = 20;

    /// <summary>
    /// Handles <c>CP_FriendRequest</c> — the buddy list (ports <c>ReqSub_FriendRequest</c>): add a
    /// friend (they get a hidden pending entry + the invite popup), accept (the hidden entry turns
    /// visible on both sides), delete/decline, and reload. Adding is online-only for now (there is
    /// no by-name character lookup for offline players yet).
    /// </summary>
    private async ValueTask HandleFriendRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        byte flag = packet.ReadByte();
        switch (flag)
        {
            case FriendReqLoad:
                await SendBuddyListAsync(session, c, ChannelPackets.FriendLoadDone).ConfigureAwait(false);
                break;

            case FriendReqSet:
            {
                string name = packet.ReadString();
                string tag = packet.Remaining > 0 ? packet.ReadString() : string.Empty;
                if (tag.Length == 0)
                {
                    tag = ChannelPackets.DefaultBuddyTag;
                }

                if (c.Buddies.Count >= BuddyCapacity)
                {
                    await session.SendAsync(_packets.BuddyMessage(ChannelPackets.FriendSetFullMe)).ConfigureAwait(false);
                    return;
                }

                FieldPlayer? target = _fields.FindPlayerByName(name);
                Character? t = target?.Character ?? _characters.FindByName(name);
                if (t is null || t.Id == c.Id)
                {
                    await session.SendAsync(_packets.BuddyMessage(ChannelPackets.FriendSetUnknownUser)).ConfigureAwait(false);
                    return;
                }

                if (c.Buddies.ContainsKey(t.Id))
                {
                    await session.SendAsync(_packets.BuddyMessage(ChannelPackets.FriendSetAlready)).ConfigureAwait(false);
                    return;
                }

                if (t.Buddies.Count >= BuddyCapacity)
                {
                    await session.SendAsync(_packets.BuddyMessage(ChannelPackets.FriendSetFullOther)).ConfigureAwait(false);
                    return;
                }

                // The target gets a hidden pending entry; when online they also get the invite popup
                // now (an offline target sees it on their next login).
                t.Buddies[c.Id] = new BuddyEntry(c.Name, ChannelPackets.DefaultBuddyTag, Hidden: true);
                _characters.Save(t);
                if (target is not null)
                {
                    await TrySendAsync(target, BuildBuddyList(t, ChannelPackets.FriendSetDone)).ConfigureAwait(false);
                    await TrySendAsync(target, _packets.BuddyInvite(c.Id, c.Name, c.Level, c.Job)).ConfigureAwait(false);
                }

                c.Buddies[t.Id] = new BuddyEntry(t.Name, tag, Hidden: false);
                _characters.Save(c);
                await SendBuddyListAsync(session, c, ChannelPackets.FriendSetDone).ConfigureAwait(false);
                break;
            }

            case FriendReqAccept:
            {
                int friendId = packet.ReadInt();
                if (!c.Buddies.TryGetValue(friendId, out BuddyEntry? pending) || !pending.Hidden)
                {
                    return;
                }

                c.Buddies[friendId] = pending with { Hidden = false };
                _characters.Save(c);
                await SendBuddyListAsync(session, c, ChannelPackets.FriendSetDone).ConfigureAwait(false);

                if (FindOnlinePlayer(friendId) is { } friend)
                {
                    await TrySendAsync(friend, BuildBuddyList(friend.Character, ChannelPackets.FriendSetDone)).ConfigureAwait(false);
                }

                break;
            }

            case FriendReqDelete:
            {
                int friendId = packet.ReadInt();
                if (!c.Buddies.Remove(friendId))
                {
                    return;
                }

                _characters.Save(c);
                await SendBuddyListAsync(session, c, ChannelPackets.FriendDeleteDone).ConfigureAwait(false);

                // The other side's entry stays but now shows this player as offline.
                if (FindOnlinePlayer(friendId) is { } friend && friend.Character.Buddies.ContainsKey(c.Id))
                {
                    await TrySendAsync(friend, _packets.BuddyChannelUpdate(c.Id, -1)).ConfigureAwait(false);
                }

                break;
            }
        }
    }

    /// <summary>An online player by character id across the channel's fields, or null.</summary>
    private FieldPlayer? FindOnlinePlayer(int characterId)
    {
        foreach (Field field in _fields.Fields)
        {
            foreach (FieldPlayer player in field.Players)
            {
                if (player.Character.Id == characterId)
                {
                    return player;
                }
            }
        }

        return null;
    }

    private byte[] BuildBuddyList(Character c, byte flag)
    {
        var rows = new List<ChannelPackets.BuddyRow>(c.Buddies.Count);
        foreach ((int id, BuddyEntry entry) in c.Buddies)
        {
            int channel = FindOnlinePlayer(id) is null ? -1 : 0;
            rows.Add(new ChannelPackets.BuddyRow(id, entry.Name, entry.Tag, entry.Hidden, channel));
        }

        return _packets.BuddyListResult(flag, rows);
    }

    private async ValueTask SendBuddyListAsync(MapleSession session, Character c, byte flag)
        => await session.SendAsync(BuildBuddyList(c, flag)).ConfigureAwait(false);

    /// <summary>Tells everyone who lists this player as a buddy that their channel changed.</summary>
    private async ValueTask NotifyBuddiesOfPresenceAsync(int characterId, int channel)
    {
        foreach (Field field in _fields.Fields)
        {
            foreach (FieldPlayer player in field.Players)
            {
                if (player.Character.Id != characterId
                    && player.Character.Buddies.TryGetValue(characterId, out BuddyEntry? entry)
                    && !entry.Hidden)
                {
                    await TrySendAsync(player, _packets.BuddyChannelUpdate(characterId, channel)).ConfigureAwait(false);
                }
            }
        }
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

        await session.SendAsync(_packets.CharacterInfo(target.Character)).ConfigureAwait(false);
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
        StatFlag changed = CharacterProgression.GainExp(c, exp); // processes level-ups
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
        await session.SendAsync(_packets.UserSitResult(seatId)).ConfigureAwait(false);
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
            FieldPlayer? target = _fields.FindPlayerByName(targetName);
            await session.SendAsync(
                _packets.WhisperLocationResult(targetName, target?.Character.MapId ?? 0, target is not null))
                .ConfigureAwait(false);
            return;
        }

        if (op == WpWhisperOp)
        {
            string targetName = packet.ReadString();
            string message = packet.ReadString();
            FieldPlayer? target = _fields.FindPlayerByName(targetName);

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
                if (party is not null)
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
                byte[] joinPacket = _packets.PartyJoin(target.Id, _player.Character.Name, target.ViewSlots(PartyChannel), target.LeaderId, PartyChannel);
                await PartyBroadcastAsync(target, joinPacket).ConfigureAwait(false);
                await SyncPartyHpAsync(target, _player).ConfigureAwait(false);
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
                byte[] expel = _packets.PartyDepart(party.Id, kickId, kicked.Character.Name, PartyDepart.Expel, party.ViewSlots(PartyChannel), party.LeaderId, PartyChannel);
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
            byte[] disband = _packets.PartyDepart(party.Id, leaverId, leaverName, PartyDepart.Disband, party.ViewSlots(PartyChannel), party.LeaderId, PartyChannel);
            await PartyBroadcastAsync(party, disband).ConfigureAwait(false);
            _parties.Disband(party);
            return;
        }

        party.Remove(leaverId);
        _parties.Unregister(leaverId);
        byte[] leave = _packets.PartyDepart(party.Id, leaverId, leaverName, PartyDepart.Leave, party.ViewSlots(PartyChannel), party.LeaderId, PartyChannel);
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

        byte[] refresh = _packets.PartyRefresh(party.Id, party.ViewSlots(PartyChannel), party.LeaderId, PartyChannel, loading: false);
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

    /// <summary>
    /// Minimal GM/debug command set for local testing (chat lines starting with '/'). Replies
    /// are echoed back to the caller as their own chat line. Documented in docs/COMMANDS.md
    /// (Japanese: docs/COMMANDS.ja.md) — keep those in sync when commands change.
    /// </summary>
    private async ValueTask HandleCommandAsync(MapleSession session, string command)
    {
        string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "map" when parts.Length >= 2 && int.TryParse(parts[1], out int mapId):
                await MovePlayerToMapAsync(session, mapId, spawnPortal: 0).ConfigureAwait(false);
                break;

            case "meso" when parts.Length >= 2 && int.TryParse(parts[1], out int amount):
                _player!.Character.Meso = (int)Math.Clamp((long)_player.Character.Meso + amount, 0, int.MaxValue);
                _characters.Save(_player.Character);
                await session.SendAsync(_packets.StatChanged(_player.Character, StatFlag.Meso)).ConfigureAwait(false);
                break;

            case "notice" when parts.Length >= 2:
                await _field!.BroadcastAsync(_packets.BroadcastNotice(command["notice ".Length..].Trim()))
                    .ConfigureAwait(false);
                break;

            case "heal":
            {
                Character hc = _player!.Character;
                hc.Hp = hc.MaxHp;
                hc.Mp = hc.MaxMp;
                await session.SendAsync(_packets.StatChanged(hc, StatFlag.Hp | StatFlag.Mp)).ConfigureAwait(false);
                await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false); // party sees the heal
                break;
            }

            case "job" when parts.Length >= 2 && int.TryParse(parts[1], out int job):
                await SetStatAsync(session, StatFlag.Job, c => c.Job = (short)job).ConfigureAwait(false);
                break;

            case "level" when parts.Length >= 2 && int.TryParse(parts[1], out int level):
            {
                Character lc = _player!.Character;
                lc.Level = (byte)Math.Clamp(level, 1, 200);
                lc.Exp = 0; // reset so the new level's bar starts clean
                _characters.Save(lc);
                await session.SendAsync(_packets.StatChanged(lc, StatFlag.Level | StatFlag.Exp)).ConfigureAwait(false);
                await RefreshPartyWindowAsync(_player).ConfigureAwait(false); // party window shows levels
                break;
            }

            case "hp" when parts.Length >= 2 && int.TryParse(parts[1], out int hp):
            {
                Character sc = _player!.Character;
                sc.Hp = (short)Math.Clamp(hp, 0, sc.MaxHp);
                _characters.Save(sc);
                await session.SendAsync(_packets.StatChanged(sc, StatFlag.Hp)).ConfigureAwait(false);
                await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false);
                break;
            }

            case "maxhp" when parts.Length >= 2 && int.TryParse(parts[1], out int maxHp):
            {
                Character sc = _player!.Character;
                sc.MaxHp = (short)Math.Clamp(maxHp, 1, 30000);
                sc.Hp = Math.Min(sc.Hp, sc.MaxHp);
                _characters.Save(sc);
                await session.SendAsync(_packets.StatChanged(sc, StatFlag.Hp | StatFlag.MaxHp)).ConfigureAwait(false);
                await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false);
                break;
            }

            case "mp" when parts.Length >= 2 && int.TryParse(parts[1], out int mp):
                await SetStatAsync(session, StatFlag.Mp, c => c.Mp = (short)Math.Clamp(mp, 0, c.MaxMp)).ConfigureAwait(false);
                break;

            case "maxmp" when parts.Length >= 2 && int.TryParse(parts[1], out int maxMp):
                await SetStatAsync(session, StatFlag.Mp | StatFlag.MaxMp, c =>
                {
                    c.MaxMp = (short)Math.Clamp(maxMp, 1, 30000);
                    c.Mp = Math.Min(c.Mp, c.MaxMp);
                }).ConfigureAwait(false);
                break;

            case "str" when parts.Length >= 2 && int.TryParse(parts[1], out int str):
                await SetStatAsync(session, StatFlag.Str, c => c.Str = (short)Math.Clamp(str, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "dex" when parts.Length >= 2 && int.TryParse(parts[1], out int dex):
                await SetStatAsync(session, StatFlag.Dex, c => c.Dex = (short)Math.Clamp(dex, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "int" when parts.Length >= 2 && int.TryParse(parts[1], out int intStat):
                await SetStatAsync(session, StatFlag.Int, c => c.Int = (short)Math.Clamp(intStat, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "luk" when parts.Length >= 2 && int.TryParse(parts[1], out int luk):
                await SetStatAsync(session, StatFlag.Luk, c => c.Luk = (short)Math.Clamp(luk, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "ap" when parts.Length >= 2 && int.TryParse(parts[1], out int ap):
                await SetStatAsync(session, StatFlag.Ap, c => c.Ap = (short)Math.Clamp(c.Ap + ap, 0, short.MaxValue)).ConfigureAwait(false);
                break;

            case "sp" when parts.Length >= 2 && int.TryParse(parts[1], out int sp):
                await SetStatAsync(session, StatFlag.Sp, c => c.Sp = (short)Math.Clamp(c.Sp + sp, 0, short.MaxValue)).ConfigureAwait(false);
                break;

            case "fame" when parts.Length >= 2 && int.TryParse(parts[1], out int fame):
                await SetStatAsync(session, StatFlag.Fame, c => c.Fame = (short)Math.Clamp(fame, -30000, 30000)).ConfigureAwait(false);
                break;

            case "save":
                _characters.Save(_player!.Character);
                await ReplyAsync(session, "saved").ConfigureAwait(false);
                break;

            case "item" when parts.Length >= 2 && int.TryParse(parts[1], out int itemId):
            {
                int qty = parts.Length >= 3 && int.TryParse(parts[2], out int q) ? q : 1;
                Character ic = _player!.Character;
                int slotMax = _items.GetConsume(itemId)?.SlotMax ?? Inventory.DefaultSlotMax;
                List<InventoryChange> changes = Inventory.Add(ic, itemId, qty, slotMax);
                PopulateEquipStats(changes); // a spawned equip gets its wz base stats
                _characters.Save(ic);
                if (changes.Count > 0)
                {
                    await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
                }

                await ReplyAsync(session, $"added {itemId} x{qty}").ConfigureAwait(false);
                break;
            }

            case "shop" when parts.Length >= 2 && int.TryParse(parts[1], out int shopId):
            {
                Shop? shop = _shops.GetShop(shopId);
                if (shop is null)
                {
                    await ReplyAsync(session, $"no shop {shopId}").ConfigureAwait(false);
                    break;
                }

                await OpenShopAsync(session, shop).ConfigureAwait(false);
                break;
            }

            case "storage":
                await OpenStorageAsync(session).ConfigureAwait(false);
                break;

            case "warp" when parts.Length >= 2:
            {
                FieldPlayer? target = _fields.FindPlayerByName(parts[1]);
                if (target is null || target.Character.Id == _player!.Character.Id)
                {
                    await ReplyAsync(session, $"'{parts[1]}' is not online").ConfigureAwait(false);
                    break;
                }

                await MovePlayerToMapAsync(session, target.Character.MapId, spawnPortal: 0).ConfigureAwait(false);
                break;
            }

            case "players":
            case "online":
            {
                var names = new List<string>();
                foreach (Field f in _fields.Fields)
                {
                    foreach (FieldPlayer fp in f.Players)
                    {
                        names.Add(fp.Character.Name);
                    }
                }

                await ReplyAsync(session, "online: " + (names.Count == 0 ? "(none)" : string.Join(", ", names)))
                    .ConfigureAwait(false);
                break;
            }

            case "pos":
                await ReplyAsync(session, $"pos: ({_player!.X}, {_player.Y}) map {_player.Character.MapId}")
                    .ConfigureAwait(false);
                break;

            case "help":
                await ReplyAsync(session, "commands: /map <id>, /warp <name>, /meso <n>, /heal, /job <n>, /level <n>, "
                    + "/hp /maxhp /mp /maxmp /str /dex /int /luk <n>, /ap <n>, /sp <n>, /fame <n>, "
                    + "/item <id> [qty], /shop <id>, /storage, /save, /players, /notice <msg>, /pos, /help")
                    .ConfigureAwait(false);
                break;

            default:
                await ReplyAsync(session, $"unknown command: {parts[0]}").ConfigureAwait(false);
                break;
        }
    }

    /// <summary>Applies a stat mutation to the caller, persists it, and pushes the changed stat.</summary>
    private async ValueTask SetStatAsync(MapleSession session, StatFlag flag, Action<Character> mutate)
    {
        Character c = _player!.Character;
        mutate(c);
        _characters.Save(c);
        await session.SendAsync(_packets.StatChanged(c, flag)).ConfigureAwait(false);
    }

    /// <summary>Sends a chat line visible only to the calling player (as their own message).</summary>
    private ValueTask ReplyAsync(MapleSession session, string text)
        => session.SendAsync(_packets.UserChat(_player!.Character.Id, isGm: true, text, onlyBalloon: false));

    private async ValueTask HandleSelectNpcAsync(MapleSession session, PacketReader packet)
    {
        // One conversation at a time; ignore a new NPC while a script is still running.
        if (_player is null || _conversation is { IsEnded: false })
        {
            return;
        }

        // JMS v186 CP_UserSelectNpc: [npcObjectId:4][x:2][y:2]. The client sends the runtime
        // object id; resolve it to the template id (the script/shop key) via the field.
        int objectId = packet.ReadInt();
        int templateId = _field?.FindNpc(objectId)?.TemplateId ?? objectId;

        // A vendor NPC opens its shop directly on click (ports MapleNPC.sendShop's auto-shop).
        Shop? shop = _shops.GetShopByNpc(templateId);
        if (shop is not null)
        {
            await OpenShopAsync(session, shop).ConfigureAwait(false);
            return;
        }

        if (_npcScripts is null)
        {
            return;
        }

        var dialog = new ChannelNpcDialog(session, _packets);
        _conversation = _npcScripts.Start(templateId, dialog, CreateScriptPlayer(session));
    }

    /// <summary>The <c>player</c> object handed to NPC / quest / portal scripts.</summary>
    private ChannelPlayer CreateScriptPlayer(MapleSession session) => new(
        _player!.Character, _characters, session, _packets,
        warp: (map, portal) => MovePlayerToMapAsync(session, map, portal),
        openShop: shopId => _shops.GetShop(shopId) is { } s ? OpenShopAsync(session, s) : ValueTask.CompletedTask,
        openStorage: () => OpenStorageAsync(session),
        gainItem: (itemId, quantity) => ScriptGainItemAsync(session, itemId, quantity),
        itemCount: itemId => CountInventoryItem(_player!.Character, itemId));

    /// <summary>
    /// Gives (positive) or takes (negative) items on behalf of a script, pushing the live
    /// inventory update (the script-side equivalent of a quest act's item list).
    /// </summary>
    private async ValueTask ScriptGainItemAsync(MapleSession session, int itemId, int quantity)
    {
        if (_player is null || quantity == 0)
        {
            return;
        }

        Character c = _player.Character;
        List<InventoryChange> changes;
        if (quantity > 0)
        {
            int slotMax = _items.GetConsume(itemId)?.SlotMax ?? Inventory.DefaultSlotMax;
            changes = Inventory.Add(c, itemId, quantity, slotMax);
            PopulateEquipStats(changes); // a granted equip gets its wz base stats
        }
        else
        {
            changes = RemoveInventoryQuantity(c, itemId, -quantity);
        }

        if (changes.Count > 0)
        {
            _characters.Save(c);
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_UserPortalScriptRequest</c> — stepping on a scripted portal (ports
    /// <c>ReqCUser.OnUserPortalScriptRequest</c>). Looks up the portal on the current map and runs
    /// its script (which typically warps the player). Runs off the packet loop so a warp inside is
    /// safe. No-op if the portal has no script or scripting isn't configured.
    /// </summary>
    private async ValueTask HandlePortalScriptAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null || _portalScripts is null)
        {
            return;
        }

        // JMS v186 CP_UserPortalScriptRequest: [portalCount:1][portalName:str][x:2][y:2]
        packet.ReadByte();
        string portalName = packet.ReadString();

        PortalData? portal = _maps.GetMap(_player.Character.MapId)?.FindPortal(portalName);
        if (portal is null || !portal.HasScript)
        {
            return;
        }

        ChannelPlayer scriptPlayer = CreateScriptPlayer(session);
        await Task.Run(() => _portalScripts.Run(portal.Script, scriptPlayer)).ConfigureAwait(false);
    }

    private void HandleScriptAnswer(PacketReader packet)
    {
        NpcConversation? conversation = _conversation;
        if (conversation is null || conversation.IsEnded)
        {
            _conversation = null;
            return;
        }

        // JMS v186 CP_UserScriptMessageAnswer: [nMsgType:1][action:1][payload by type]
        int messageType = packet.ReadByte();
        int action = (sbyte)packet.ReadByte();
        int selection = -1;
        string text = string.Empty;

        if (action != 0)
        {
            switch (messageType)
            {
                case 5:  // SM_ASKMENU
                    selection = packet.ReadInt();
                    break;
                case 3:  // SM_ASKTEXT
                    text = packet.ReadString();
                    break;
                case 8:  // SM_ASKAVATAR
                    selection = packet.ReadByte();
                    break;
                case 15: // SM_ASKSLIDEMENU
                    selection = packet.ReadInt();
                    break;
            }
        }

        conversation.Advance(messageType, action, selection, text);
    }

    private async ValueTask HandleTransferFieldAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // A dead player dismissing the tombstone dialog sends a transfer request; revive them at
        // this map's return town (or in place) with full HP/MP instead of a normal transfer.
        if (_player.Character.Hp <= 0)
        {
            await ReviveAsync(session).ConfigureAwait(false);
            return;
        }

        // JMS v186 CP_UserTransferFieldRequest:
        //   [portalCount:1][mapId:4][portalName:str][x:2,y:2 if portal][unk:1][reviveType:1]
        packet.ReadByte();
        int targetMapId = packet.ReadInt();
        string portalName = packet.ReadString();

        // A direct map id (portal name empty) is a /map-style jump: honor it as-is.
        if (string.IsNullOrEmpty(portalName))
        {
            if (targetMapId < 0)
            {
                await session.SendAsync(_packets.TransferFieldReqIgnored(TransferDisabledPortal)).ConfigureAwait(false);
                return;
            }

            await MovePlayerToMapAsync(session, targetMapId, spawnPortal: 0).ConfigureAwait(false);
            return;
        }

        // Portal-by-name: look up the portal on the current map and follow its link.
        MapData? currentMap = _maps.GetMap(_player.Character.MapId);
        PortalData? portal = currentMap?.FindPortal(portalName);
        if (portal is null || !portal.LinksToMap)
        {
            await session.SendAsync(_packets.TransferFieldReqIgnored(TransferDisabledPortal)).ConfigureAwait(false);
            return;
        }

        int spawn = ResolveSpawnPortal(portal.TargetMapId, portal.TargetName);
        await MovePlayerToMapAsync(session, portal.TargetMapId, spawn).ConfigureAwait(false);
    }

    /// <summary>Finds the spawn portal id in the destination map by its target-portal name.</summary>
    private int ResolveSpawnPortal(int targetMapId, string targetPortalName)
    {
        MapData? target = _maps.GetMap(targetMapId);
        PortalData? spawn = string.IsNullOrEmpty(targetPortalName)
            ? target?.SpawnPortal
            : target?.FindPortal(targetPortalName) ?? target?.SpawnPortal;
        return spawn?.Id ?? 0;
    }

    /// <summary>
    /// Revives a dead player: restores full HP/MP, then transfers to this map's return town (or
    /// the same map when it has none), which clears the client's death state.
    /// </summary>
    private async ValueTask ReviveAsync(MapleSession session)
    {
        Character c = _player!.Character;
        c.Hp = c.MaxHp;
        c.Mp = c.MaxMp;

        int reviveMap = _maps.GetMap(c.MapId)?.ReviveMap ?? c.MapId;
        await MovePlayerToMapAsync(session, reviveMap, spawnPortal: 0).ConfigureAwait(false);
        await NotifyPartyOfMyHpAsync(_player!).ConfigureAwait(false); // party sees the revive
    }

    /// <summary>
    /// Moves the bound player to another map: leave + announce, switch fields, SetField
    /// (map-change branch), then exchange enter-field packets in the new map.
    /// </summary>
    private async ValueTask MovePlayerToMapAsync(MapleSession session, int targetMapId, int spawnPortal)
    {
        FieldPlayer player = _player!;
        Field oldField = _field!;

        oldField.Leave(player.Character.Id);
        await oldField.BroadcastAsync(_packets.UserLeaveField(player.Character.Id)).ConfigureAwait(false);
        await ReleaseControlledMobsAsync(oldField, player.Character.Id).ConfigureAwait(false);

        player.Character.MapId = targetMapId;
        player.Character.Portal = (byte)spawnPortal;
        _characters.Save(player.Character); // DB-backed repos need an explicit flush

        await session.SendAsync(_packets.SetFieldChangeMap(player.Character, _channelId)).ConfigureAwait(false);

        Field newField = _fields.Get(targetMapId);
        foreach (FieldPlayer other in newField.Players)
        {
            await session.SendAsync(_packets.UserEnterField(other)).ConfigureAwait(false);
        }

        newField.Enter(player);
        _field = newField;
        await newField.BroadcastAsync(_packets.UserEnterField(player), exceptCharacterId: player.Character.Id)
            .ConfigureAwait(false);

        await SpawnNpcsAsync(session, newField).ConfigureAwait(false);
        await RefreshPartyWindowAsync(player).ConfigureAwait(false); // party window shows the new map
    }

    /// <summary>
    /// Extracts the start position from a CMovePath buffer:
    /// <c>[startX:2][startY:2]...</c> (CMovePath::Decode reads the head as the origin point).
    /// </summary>
    private static void UpdatePositionFromMovePath(FieldPlayer player, byte[] movePath)
    {
        if (movePath.Length < 4)
        {
            return;
        }

        player.X = (short)(movePath[0] | (movePath[1] << 8));
        player.Y = (short)(movePath[2] | (movePath[3] << 8));
    }

    private static int RandomSeed() => RandomNumberGenerator.GetInt32(int.MaxValue);
}
