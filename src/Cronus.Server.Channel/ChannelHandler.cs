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
    private readonly GuildRegistry _guilds;
    private readonly MiniGameRegistry _miniGames;
    private readonly PlayerShopRegistry _playerShops;
    private readonly HiredMerchantRegistry _merchants;
    private readonly IReactorProvider? _reactors;
    private readonly PortalScriptEngine? _reactorScripts;
    private readonly INpcNameProvider? _npcNames;

    /// <summary>Valid hair/face/skin ids from game data, for salon scripts; null without wz.</summary>
    private readonly IStyleProvider? _styles;
    private readonly int _opReactorHit;
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
    private readonly int _opUpgradeItem;
    private readonly int _opPortalScroll;
    private readonly int _opPortableChair;
    private readonly int _opMacroModified;
    private readonly int _opPartyResult;
    private readonly int _opChangeStatReq;
    private readonly int _opSkillPrepare;
    private readonly int _opMobApplyCtrl;
    private readonly int _opTransferChannel;
    private readonly int _opMigrateCashShop;
    private readonly int _opCashItemUse;
    private readonly int _opActivatePet;
    private readonly int _opPetMove;
    private readonly int _opPetAction;
    private readonly int _opPetFood;
    private readonly int _opAdBoardClose;
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
    private readonly int _opGuildRequest;
    private readonly int _opGuildDeny;
    private readonly int _opGroupMessage;
    private readonly int _opGatherItem;
    private readonly int _opSortItem;

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
        BuffTracker? buffs = null,
        GuildRegistry? guilds = null,
        MiniGameRegistry? miniGames = null,
        PlayerShopRegistry? playerShops = null,
        HiredMerchantRegistry? merchants = null,
        IReactorProvider? reactors = null,
        PortalScriptEngine? reactorScripts = null,
        INpcNameProvider? npcNames = null,
        IStyleProvider? styles = null)
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
        _guilds = guilds ?? new GuildRegistry();
        _miniGames = miniGames ?? new MiniGameRegistry();
        _playerShops = playerShops ?? new PlayerShopRegistry();
        _merchants = merchants ?? new HiredMerchantRegistry();
        _reactors = reactors;
        _reactorScripts = reactorScripts;
        _npcNames = npcNames;
        _styles = styles;
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
        _opUpgradeItem = clientOpcodes.Get(ClientOpcode.UserUpgradeItemUseRequest);
        _opPortalScroll = clientOpcodes.Get(ClientOpcode.UserPortalScrollUseRequest);
        _opPortableChair = clientOpcodes.Get(ClientOpcode.UserPortableChairSitRequest);
        _opMacroModified = clientOpcodes.Get(ClientOpcode.UserMacroSysDataModified);
        _opPartyResult = clientOpcodes.Get(ClientOpcode.PartyResult);
        _opChangeStatReq = clientOpcodes.Get(ClientOpcode.UserChangeStatRequest);
        _opSkillPrepare = clientOpcodes.Get(ClientOpcode.UserSkillPrepareRequest);
        _opMobApplyCtrl = clientOpcodes.Get(ClientOpcode.MobApplyCtrl);
        _opTransferChannel = clientOpcodes.Get(ClientOpcode.UserTransferChannelRequest);
        _opMigrateCashShop = clientOpcodes.Get(ClientOpcode.UserMigrateToCashShopRequest);
        _opCashItemUse = clientOpcodes.Get(ClientOpcode.UserConsumeCashItemUseRequest);
        _opActivatePet = clientOpcodes.Get(ClientOpcode.UserActivatePetRequest);
        _opPetMove = clientOpcodes.Get(ClientOpcode.PetMove);
        _opPetAction = clientOpcodes.Get(ClientOpcode.PetAction);
        _opPetFood = clientOpcodes.Get(ClientOpcode.UserPetFoodItemUseRequest);
        _opAdBoardClose = clientOpcodes.Get(ClientOpcode.UserAdBoardClose);
        _opReactorHit = clientOpcodes.Get(ClientOpcode.ReactorHit);
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
        _opGuildRequest = clientOpcodes.Get(ClientOpcode.GuildRequest);
        _opGuildDeny = clientOpcodes.Get(ClientOpcode.GuildResult);
        _opGroupMessage = clientOpcodes.Get(ClientOpcode.GroupMessage);
        _opGatherItem = clientOpcodes.Get(ClientOpcode.UserGatherItemRequest);
        _opSortItem = clientOpcodes.Get(ClientOpcode.UserSortItemRequest);
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
        else if (opcode == _opUpgradeItem)
        {
            await HandleUpgradeItemAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opPortalScroll)
        {
            // Same body as a stat-change item use; the moveTo path warps (ports
            // OnUserPortalScrollUseRequest -> applyReturnScroll).
            await HandleUseItemAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opPortableChair)
        {
            await HandlePortableChairAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opMacroModified)
        {
            HandleMacroModified(packet);
        }
        else if (opcode == _opPartyResult)
        {
            await HandlePartyResultAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opChangeStatReq)
        {
            await HandleChangeStatRequestAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opSkillPrepare)
        {
            await HandleSkillPrepareAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opMobApplyCtrl)
        {
            await HandleMobApplyCtrlAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opTransferChannel)
        {
            // Single-channel server: decline so the client's channel menu unblocks.
            await session.SendAsync(_packets.TransferChannelReqIgnored(reason: 1)).ConfigureAwait(false);
        }
        else if (opcode == _opMigrateCashShop)
        {
            // No cash shop server: decline (2 = shop server unavailable).
            await session.SendAsync(_packets.TransferChannelReqIgnored(reason: 2)).ConfigureAwait(false);
        }
        else if (opcode == _opCashItemUse)
        {
            await HandleCashItemUseAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opActivatePet)
        {
            await HandleActivatePetAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opPetMove)
        {
            await HandlePetMoveAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opPetAction)
        {
            await HandlePetActionAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opPetFood)
        {
            await HandlePetFoodAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opReactorHit)
        {
            await HandleReactorHitAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opAdBoardClose)
        {
            if (_player is not null && _field is not null)
            {
                _player.AdBoard = null;
                await _field.BroadcastAsync(_packets.UserAdBoard(_player.Character.Id, null)).ConfigureAwait(false);
            }
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
        else if (opcode == _opGuildRequest)
        {
            await HandleGuildRequestAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opGuildDeny)
        {
            await HandleGuildDenyAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opGroupMessage)
        {
            await HandleGroupMessageAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opGatherItem)
        {
            await HandleGatherItemAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opSortItem)
        {
            await HandleSortItemAsync(session, packet).ConfigureAwait(false);
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

            // Leave any game room (the owner dropping closes it for the visitor too).
            if (_miniGames.GetForCharacter(_player.Character.Id) is { } miniGame)
            {
                await ExitMiniGameAsync(miniGame, _player.Character.Id).ConfigureAwait(false);
            }

            // Leave any personal shop (the owner dropping closes it and reclaims the stock).
            if (_playerShops.GetForCharacter(_player.Character.Id) is { } playerShop)
            {
                await ExitPlayerShopAsync(playerShop, _player.Character.Id).ConfigureAwait(false);
            }

            // Leave any merchant room; a managing owner dropping reopens/packs it up as on leave.
            if (_merchants.GetForParticipant(_player.Character.Id) is { } merchantRoom)
            {
                await ExitHiredMerchantAsync(merchantRoom, _player.Character.Id).ConfigureAwait(false);
            }

            // Buddies see this player go offline.
            await NotifyBuddiesOfPresenceAsync(_player.Character.Id, channel: -1).ConfigureAwait(false);

            // Buffs don't survive a logout.
            _buffs.Clear(_player.Character.Id);

            // Guildmates see this player go offline.
            if (_player.Character.GuildId > 0)
            {
                int guildId = _player.Character.GuildId;
                _guilds.SetOffline(guildId, _player.Character.Id);
                await BroadcastToGuildAsync(guildId, _packets.GuildMemberOnline(guildId, _player.Character.Id, online: false)).ConfigureAwait(false);
            }

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
        await session.SendAsync(_packets.MacroSysDataInit(character.SkillMacros)).ConfigureAwait(false);
        await session.SendAsync(BuildBuddyList(character, ChannelPackets.FriendLoadDone)).ConfigureAwait(false);
        await session.SendAsync(_packets.FamilyInfoResult()).ConfigureAwait(false);
        await session.SendAsync(_packets.BroadcastSlideClear()).ConfigureAwait(false);

        // Join the field: tell the newcomer about everyone already there, and vice versa.
        Field field = _fields.Get(character.MapId);
        foreach (FieldPlayer other in field.Players)
        {
            await session.SendAsync(_packets.UserEnterField(other, GuildOf(other.Character))).ConfigureAwait(false);
        }

        field.Enter(player);
        _field = field;
        await field.BroadcastAsync(_packets.UserEnterField(player, GuildOf(character)), exceptCharacterId: character.Id)
            .ConfigureAwait(false);

        await SpawnNpcsAsync(session, field).ConfigureAwait(false);
        await SpawnReactorsAsync(session, field).ConfigureAwait(false);

        // Open game rooms and shops in this map show their balloons to the newcomer.
        foreach (MiniGame game in _miniGames.GamesInMap(character.MapId))
        {
            await session.SendAsync(_packets.MiniRoomBalloon(game.Owner.Character.Id, game)).ConfigureAwait(false);
        }

        foreach (PlayerShop shop in _playerShops.ShopsInMap(character.MapId))
        {
            await session.SendAsync(_packets.PlayerShopBalloon(shop.Owner.Character.Id, shop)).ConfigureAwait(false);
        }

        foreach (HiredMerchant merchant in _merchants.MerchantsInMap(character.MapId))
        {
            await session.SendAsync(_packets.EmployeeEnterField(merchant)).ConfigureAwait(false);
        }

        await NotifyBuddiesOfPresenceAsync(character.Id, channel: 0).ConfigureAwait(false); // "came online"

        // Guild window data + presence; a guild that no longer exists is scrubbed off the character.
        if (character.GuildId > 0)
        {
            if (_guilds.Get(character.GuildId) is { } guild)
            {
                _guilds.SetOnline(guild.Id, player);
                await session.SendAsync(_packets.GuildInfo(guild, BuildGuildMembers(guild.Id))).ConfigureAwait(false);
                await BroadcastToGuildAsync(guild.Id, _packets.GuildMemberOnline(guild.Id, character.Id, online: true), exceptCharacterId: character.Id).ConfigureAwait(false);
                await BroadcastToGuildAsync(guild.Id, _packets.GuildMemberLevelJob(guild.Id, character.Id, character.Level, character.Job), exceptCharacterId: character.Id).ConfigureAwait(false);
            }
            else
            {
                character.GuildId = 0;
                character.GuildRank = 0;
                _characters.Save(character);
            }
        }

        // Friend requests that arrived while offline pop up now.
        foreach ((int fromId, BuddyEntry entry) in character.Buddies.ToList())
        {
            if (entry.Hidden && _characters.Find(fromId) is { } from)
            {
                await session.SendAsync(_packets.BuddyInvite(fromId, from.Name, from.Level, from.Job)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Shows the field's standing (unbroken) reactors to a newcomer.</summary>
    private async ValueTask SpawnReactorsAsync(MapleSession session, Field field)
    {
        foreach (FieldReactor reactor in field.Reactors)
        {
            if (!reactor.IsDead)
            {
                await session.SendAsync(_packets.ReactorEnterField(reactor)).ConfigureAwait(false);
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
        await UpdateComboOrbsAsync(session, attack).ConfigureAwait(false);
    }

    // Panic / Coma variants consume the charged combo orbs.
    private static bool ConsumesComboOrbs(int skillId) => skillId is 1111003 or 1111004 or 1111005 or 1111006;

    /// <summary>
    /// Charges (one per landed swing) or consumes (Panic/Coma) Crusader combo orbs, re-sending the
    /// ComboCounter temporary stat with the new count (value = orbs + 1). The reference declares
    /// the CTS bit but never tracks orbs; this uses the already-verified stat-set layout.
    /// </summary>
    private async ValueTask UpdateComboOrbsAsync(MapleSession session, AttackInfo attack)
    {
        Character c = _player!.Character;
        ActiveBuff? combo = _buffs.Find(c.Id, SkillBuff.ComboAttackSkill);
        if (combo is null)
        {
            _player.ComboOrbs = 0;
            return;
        }

        int level = c.Skills.TryGetValue(SkillBuff.ComboAttackSkill, out int lvl) ? lvl : 1;
        int maxOrbs = _skills.GetSkillEffect(SkillBuff.ComboAttackSkill, level)?.X ?? 5;

        int orbs = _player.ComboOrbs;
        if (ConsumesComboOrbs(attack.SkillId))
        {
            orbs = 0;
        }
        else if (attack.Targets.Count > 0)
        {
            orbs = Math.Min(maxOrbs, orbs + 1);
        }

        if (orbs == _player.ComboOrbs)
        {
            return;
        }

        _player.ComboOrbs = orbs;
        int remainingMs = (int)Math.Max(0, (combo.ExpiresAt - DateTime.UtcNow).TotalMilliseconds);
        var stat = new List<BuffStat>
        {
            new(SkillBuff.ComboCounter, (short)(orbs + 1), SkillBuff.ComboAttackSkill, remainingMs),
        };
        await session.SendAsync(_packets.TemporaryStatSet(stat)).ConfigureAwait(false);
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

        // The controller signalled the mob may act: the server picks a castable skill (ports
        // MobUsesSkill) and answers it in the ack so the client animates the cast.
        (byte ackSkill, byte ackLevel) = nextAttackPossible
            ? await TryCastMobSkillAsync(mob).ConfigureAwait(false)
            : ((byte)0, (byte)0);

        await session.SendAsync(_packets.MobCtrlAck(mob, moveId, aggro: false, ackSkill, ackLevel)).ConfigureAwait(false);
        await _field.BroadcastAsync(
            _packets.MobMove(mob.ObjectId, nextAttackPossible, left, mobSkill, movePath),
            exceptCharacterId: characterId).ConfigureAwait(false);
    }

    /// <summary>
    /// Picks and applies one of the mob's wz skills (ports <c>MobUsesSkill</c> + the working scope
    /// of <c>MobSkill.applyEffect</c>): a random known skill, gated by its cooldown and the mob's
    /// HP%% threshold. Self-heal (114) restores HP with a green number; summon (200) spawns the
    /// skill's mobs at the caster (capped by the wz limit). Returns the skill to ack, or (0,0).
    /// </summary>
    private async ValueTask<(byte SkillId, byte Level)> TryCastMobSkillAsync(FieldMob mob)
    {
        if (_field is null || _fields.MobProvider?.GetMob(mob.TemplateId) is not { } stats || stats.Skills.Count == 0)
        {
            return (0, 0);
        }

        MobSkillEntry pick = stats.Skills[Random.Shared.Next(stats.Skills.Count)];
        if (_skills.GetMobSkill(pick.SkillId, pick.Level) is not { } mobSkill)
        {
            return (0, 0);
        }

        long now = Environment.TickCount64;
        if (mob.LastSkillUse.TryGetValue(pick.SkillId, out long last) && now - last <= mobSkill.IntervalMs)
        {
            return (0, 0); // still cooling down
        }

        if (mob.MaxHp > 0 && mob.Hp * 100L / mob.MaxHp > mobSkill.HpThresholdPercent)
        {
            return (0, 0); // not hurt enough to cast
        }

        mob.LastSkillUse[pick.SkillId] = now;
        mob.Mp = (short)Math.Max(0, mob.Mp - mobSkill.MpCon);

        switch (pick.SkillId)
        {
            case 114: // self-heal: green number + HP back
            {
                int healed = mob.Heal(mobSkill.X);
                if (healed > 0)
                {
                    await _field.BroadcastAsync(_packets.MobDamaged(mob, -healed)).ConfigureAwait(false);
                }

                break;
            }

            case 200: // summon minions at the caster, up to the wz field cap
            {
                int alive = _field.Mobs.Count(m => !m.IsDead);
                foreach (int summonId in mobSkill.Summons)
                {
                    if (mobSkill.Limit > 0 && alive >= mobSkill.Limit)
                    {
                        break;
                    }

                    MobData? summonStats = _fields.MobProvider?.GetMob(summonId);
                    FieldMob summon = _field.SpawnMob(summonId, summonStats, mob.X, mob.Y, mob.Foothold);
                    alive++;
                    await _field.BroadcastAsync(_packets.MobEnterField(summon)).ConfigureAwait(false);

                    // Delegate the new mob's AI to this controller's client.
                    summon.ControllerId = _player!.Character.Id;
                    await TrySendAsync(_player, _packets.MobChangeController(summon)).ConfigureAwait(false);
                }

                break;
            }
        }

        return ((byte)pick.SkillId, (byte)pick.Level);
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

        StatFlag changed = CharacterProgression.SpendAbilityPoint(_player.Character, stat, EffectResolverFor(_player.Character));
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
        ulong mask = BuffEffect.Mask64(buffs);
        _buffs.Register(c.Id, skillId, mask, effect.DurationMs); // state before the packet
        if (skillId == SkillBuff.ComboAttackSkill)
        {
            _player.ComboOrbs = 0; // a fresh combo starts uncharged
        }

        await session.SendAsync(buffPacket).ConfigureAwait(false);

        // A party buff (Haste, Rage, Hyper Body, … — marked by the wz affect box) also lands on
        // party members in the same map (ports the isPartyBuff apply; range box simplified to map).
        if (effect.HasPartyArea && _parties.GetForCharacter(c.Id) is { } party)
        {
            foreach (FieldPlayer member in party.Members)
            {
                if (member.Character.Id != c.Id && member.Character.MapId == c.MapId)
                {
                    _buffs.Register(member.Character.Id, skillId, mask, effect.DurationMs);
                    await TrySendAsync(member, buffPacket).ConfigureAwait(false);
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

        ulong mask = BuffEffect.Mask64(SkillBuff.FromEffect(skillId, effect));
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
                _buffs.Register(c.Id, -spec.ItemId, BuffEffect.Mask64(buffs), spec.Time); // state first
                await session.SendAsync(_packets.TemporaryStatSet(buffs)).ConfigureAwait(false);
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

        ulong mask = BuffEffect.Mask64(BuffEffect.FromSpec(spec));
        if (mask != 0)
        {
            _buffs.Remove(_player.Character.Id, buffId);
            await session.SendAsync(_packets.TemporaryStatReset(mask)).ConfigureAwait(false);
        }
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
            return;
        }

        bool cleanSlate = Scrolling.IsCleanSlate(scroll.ItemId);
        bool chaos = Scrolling.IsChaosScroll(scroll.ItemId);
        if (!cleanSlate && equip.UpgradeSlots < 1)
        {
            return; // nothing left to scroll
        }

        if (!cleanSlate && !chaos && !Scrolling.CanScroll(scroll.ItemId, equip.ItemId))
        {
            return; // scroll targets a different equip family
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

        Character c = _player!.Character;
        long price = (long)entry.Price * quantity;
        if (entry.Price < 0 || c.Meso < price)
        {
            await session.SendAsync(_packets.ShopResult(ShopResultCode.BuyNoMoney)).ConfigureAwait(false);
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
    private const byte QuestReqLostItem = 0;
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
            case QuestReqLostItem:
            {
                // [time:4][itemId:4] — re-grant a lost quest item the start act originally gave
                // (ports MapleQuestAction.RestoreLostItem: only if the player no longer has one).
                packet.ReadInt();
                int itemId = packet.ReadInt();
                QuestData? quest = _quests.GetQuest(questId);
                if (quest?.StartAct is { } act
                    && act.Items.Any(i => i.ItemId == itemId)
                    && CountInventoryItem(c, itemId) < 1)
                {
                    await ScriptGainItemAsync(session, itemId, 1).ConfigureAwait(false);
                }

                break;
            }

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
    /// Handles <c>CP_MiniRoom</c> (ports <c>ReqCMiniRoomBaseDlg</c>): the trade room (type 3,
    /// <c>MapleTrade</c>) and the Omok / match-card game rooms (types 1/2, <c>MapleMiniGame</c>).
    /// Personal/hired shops (types 4/5) aren't modelled.
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
                if (roomType is MiniGame.TypeOmok or MiniGame.TypeMatchCard)
                {
                    await CreateMiniGameAsync(session, c, roomType, packet).ConfigureAwait(false);
                    return;
                }

                if (roomType == 4)
                {
                    await CreatePlayerShopAsync(session, c, packet).ConfigureAwait(false);
                    return;
                }

                if (roomType == 5)
                {
                    await CreateHiredMerchantAsync(session, c, packet).ConfigureAwait(false);
                    return;
                }

                if (roomType != 3 || _trades.Get(c.Id) is not null)
                {
                    return; // trades, game rooms, shops, and merchants; one room at a time
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
                if (trade is not null)
                {
                    if (trade.VisitorEntered || trade.InvitedCharacterId != c.Id)
                    {
                        return;
                    }

                    trade.Join(_player);
                    trade.VisitorEntered = true;
                    await session.SendAsync(_packets.TradeStart(c, 1, trade.Starter.Player.Character)).ConfigureAwait(false);
                    await TrySendAsync(trade.Starter.Player, _packets.TradePartnerAdd(c)).ConfigureAwait(false);
                    return;
                }

                await EnterMiniRoomAsync(session, c, packet).ConfigureAwait(false);
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
                else if (_miniGames.GetForCharacter(c.Id) is { } game && game.SeatOf(c.Id) is int seat and >= 0)
                {
                    await BroadcastToMiniGameAsync(game, _packets.TradeChat((byte)seat, $"{c.Name} : {message}")).ConfigureAwait(false);
                }
                else if (_playerShops.GetForCharacter(c.Id) is { } shop && shop.SeatOf(c.Id) is int shopSeat and >= 0)
                {
                    await BroadcastToPlayerShopAsync(shop, _packets.TradeChat((byte)shopSeat, $"{c.Name} : {message}")).ConfigureAwait(false);
                }
                else if (_merchants.GetForParticipant(c.Id) is { } merchant && merchant.SeatOf(c.Id) is int merchSeat and >= 0)
                {
                    await BroadcastToMerchantAsync(merchant, _packets.TradeChat((byte)merchSeat, $"{c.Name} : {message}")).ConfigureAwait(false);
                }

                break;
            }

            case ChannelPackets.MiniRoomLeave:
            {
                if (_trades.Get(c.Id) is { } trade)
                {
                    await CancelTradeAsync(trade).ConfigureAwait(false);
                }
                else if (_miniGames.GetForCharacter(c.Id) is { } game)
                {
                    await ExitMiniGameAsync(game, c.Id).ConfigureAwait(false);
                }
                else if (_playerShops.GetForCharacter(c.Id) is { } shop)
                {
                    await ExitPlayerShopAsync(shop, c.Id).ConfigureAwait(false);
                }
                else if (_merchants.GetForParticipant(c.Id) is { } merchant)
                {
                    await ExitHiredMerchantAsync(merchant, c.Id).ConfigureAwait(false);
                }

                break;
            }

            case ChannelPackets.MiniRoomBalloonReq:
                if (_merchants.GetForParticipant(c.Id) is { } stocked && stocked.SeatOf(c.Id) == 0)
                {
                    await OpenHiredMerchantForBusinessAsync(stocked).ConfigureAwait(false);
                }
                else
                {
                    await OpenPlayerShopForBusinessAsync(c).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.PsPutItem:
            case ChannelPackets.EsPutItem:
                await HandleShopPutItemAsync(session, c, packet).ConfigureAwait(false);
                break;

            case ChannelPackets.PsBuyItem:
            case ChannelPackets.EsBuyItem:
            case ChannelPackets.EsBuyResult:
                if (_merchants.GetForParticipant(c.Id) is { } sellingMerchant)
                {
                    await HandleMerchantBuyItemAsync(session, c, sellingMerchant, packet).ConfigureAwait(false);
                }
                else
                {
                    await HandleShopBuyItemAsync(session, c, packet).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.PsMoveItemToInventory:
            case ChannelPackets.EsMoveItemToInventory:
                if (_merchants.GetForParticipant(c.Id) is { } managedMerchant)
                {
                    await HandleMerchantReclaimItemAsync(session, c, managedMerchant, packet).ConfigureAwait(false);
                }
                else
                {
                    await HandleShopReclaimItemAsync(session, c, packet).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.PsBan:
                await HandleShopBanAsync(c, packet).ConfigureAwait(false);
                break;

            case ChannelPackets.TradePutItem:
                await HandleTradePutItemAsync(session, c, packet).ConfigureAwait(false);
                break;

            case ChannelPackets.TradePutMoney:
                await HandleTradePutMoneyAsync(session, c, packet).ConfigureAwait(false);
                break;

            case ChannelPackets.TradeConfirm:
                await HandleTradeConfirmAsync(session, c).ConfigureAwait(false);
                break;

            default:
                await HandleMiniGameOpAsync(session, c, protocol, packet).ConfigureAwait(false);
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

    /// <summary>
    /// Creates an Omok / match-card room (ports the MRP_Create game branch): needs the board item
    /// (4080000+n stone set / 4080100 card deck), one room per player. The room opens for the owner
    /// and its balloon appears over their head for the whole map.
    /// </summary>
    private async ValueTask CreateMiniGameAsync(MapleSession session, Character c, byte gameType, PacketReader packet)
    {
        string description = packet.ReadString();
        byte hasPassword = packet.ReadByte();
        string password = hasPassword > 0 ? packet.ReadString() : string.Empty;
        int piece = packet.ReadByte();

        int itemId = gameType == MiniGame.TypeOmok ? 4080000 + piece : 4080100;
        if (_miniGames.GetForCharacter(c.Id) is not null
            || _trades.Get(c.Id) is not null
            || CountInventoryItem(c, itemId) < 1)
        {
            return;
        }

        MiniGame game = _miniGames.Create(gameType, _player!, description, password, piece);
        await session.SendAsync(_packets.MiniGameRoom(game, viewerSeat: 0)).ConfigureAwait(false);
        if (_field is not null)
        {
            await _field.BroadcastAsync(_packets.MiniRoomBalloon(c.Id, game)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Joins a game room or a personal shop from its balloon (ports the MRP_Enter branch): a free
    /// seat if the room is open (password-gated for games); everyone sees the updated room.
    /// </summary>
    private async ValueTask EnterMiniRoomAsync(MapleSession session, Character c, PacketReader packet)
    {
        int objectId = packet.ReadInt();
        if (_miniGames.GetForCharacter(c.Id) is not null || _playerShops.GetForCharacter(c.Id) is not null)
        {
            await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
            return;
        }

        if (_miniGames.Get(objectId) is { } game)
        {
            if (game.Visitor is not null || !game.Open || game.SeatOf(c.Id) >= 0)
            {
                await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
                return;
            }

            if (game.Password.Length > 0)
            {
                byte hasPassword = packet.Remaining > 0 ? packet.ReadByte() : (byte)0;
                string password = hasPassword > 0 ? packet.ReadString() : string.Empty;
                if (!string.Equals(password, game.Password, StringComparison.Ordinal))
                {
                    await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
                    return;
                }
            }

            _miniGames.SetVisitor(game, _player!);
            await TrySendAsync(game.Owner, _packets.MiniGameNewVisitor(game, c, seat: 1)).ConfigureAwait(false);
            await session.SendAsync(_packets.MiniGameRoom(game, viewerSeat: 1)).ConfigureAwait(false);
            if (_field is not null)
            {
                await _field.BroadcastAsync(_packets.MiniRoomBalloon(game.Owner.Character.Id, game)).ConfigureAwait(false);
            }

            return;
        }

        if (_playerShops.Get(objectId) is { } shop)
        {
            int seat = shop.FreeSeat();
            if (!shop.Open || seat < 0 || shop.SeatOf(c.Id) >= 0)
            {
                await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
                return;
            }

            byte[] visitorAdd = _packets.PlayerShopVisitorAdd(c, seat);
            await BroadcastToPlayerShopAsync(shop, visitorAdd).ConfigureAwait(false);
            _playerShops.SetVisitor(shop, seat, _player!);
            await session.SendAsync(_packets.PlayerShopRoom(shop, seat)).ConfigureAwait(false);
            await UpdatePlayerShopBalloonAsync(shop).ConfigureAwait(false);
            return;
        }

        if (_merchants.Get(objectId) is { } merchant)
        {
            if (merchant.OwnerId == c.Id)
            {
                // The owner opens management: browsing visitors are shown the door first.
                for (int s = 1; s <= HiredMerchant.MaxVisitors; s++)
                {
                    if (merchant.Visitors[s - 1] is { } visitor)
                    {
                        await TrySendAsync(visitor, _packets.HiredMerchantMaintenance((byte)s)).ConfigureAwait(false);
                        _merchants.RemoveVisitor(merchant, s);
                    }
                }

                merchant.Open = false;
                _merchants.SetManager(merchant, _player!);
                await session.SendAsync(_packets.HiredMerchantRoom(merchant, viewerSeat: 0, firstTime: false)).ConfigureAwait(false);
                return;
            }

            int seat = merchant.FreeSeat();
            if (!merchant.Open || seat < 0 || merchant.SeatOf(c.Id) >= 0)
            {
                await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
                return;
            }

            byte[] visitorAdd = _packets.PlayerShopVisitorAdd(c, seat);
            await BroadcastToMerchantAsync(merchant, visitorAdd).ConfigureAwait(false);
            _merchants.SetVisitor(merchant, seat, _player!);
            await session.SendAsync(_packets.HiredMerchantRoom(merchant, seat, firstTime: false)).ConfigureAwait(false);
            Field merchantField = _fields.Get(merchant.MapId);
            await merchantField.BroadcastAsync(_packets.EmployeeMiniRoomBalloon(merchant)).ConfigureAwait(false);
            return;
        }

        await session.SendAsync(_packets.MiniGameFull()).ConfigureAwait(false);
    }

    /// <summary>Sends a packet to both seats of a game room.</summary>
    private async ValueTask BroadcastToMiniGameAsync(MiniGame game, byte[] packet)
    {
        await TrySendAsync(game.Owner, packet).ConfigureAwait(false);
        if (game.Visitor is { } visitor)
        {
            await TrySendAsync(visitor, packet).ConfigureAwait(false);
        }
    }

    /// <summary>Refreshes the room's balloon for the owner's map.</summary>
    private async ValueTask UpdateMiniGameBalloonAsync(MiniGame game, bool closed = false)
    {
        Field field = _fields.Get(game.Owner.Character.MapId);
        await field.BroadcastAsync(_packets.MiniRoomBalloon(game.Owner.Character.Id, closed ? null : game)).ConfigureAwait(false);
    }

    /// <summary>
    /// A participant leaves the room (ports <c>MapleMiniGame.exit</c>): the owner leaving closes
    /// the room for everyone; a visitor leaving frees their seat. An abandoned round ends.
    /// </summary>
    private async ValueTask ExitMiniGameAsync(MiniGame game, int leavingCharacterId)
    {
        if (game.SeatOf(leavingCharacterId) == 0)
        {
            // Owner closes the room: the visitor is told the room is closing.
            if (game.Visitor is { } visitor)
            {
                await TrySendAsync(visitor, _packets.MiniRoomClosed(1, reason: 3)).ConfigureAwait(false);
            }

            _miniGames.Remove(game);
            await UpdateMiniGameBalloonAsync(game, closed: true).ConfigureAwait(false);
        }
        else
        {
            _miniGames.RemoveVisitor(game);
            game.Ready[1] = false;
            game.Open = true; // a running round is abandoned
            await TrySendAsync(game.Owner, _packets.MiniRoomVisitorLeave(1)).ConfigureAwait(false);
            await UpdateMiniGameBalloonAsync(game).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ends a round (ports <c>getMiniGameResult</c>'s stat updates + <c>checkExitAfterGame</c>):
    /// records the result, shows it to both seats, reopens the lobby, and honors "leave after game".
    /// </summary>
    private async ValueTask EndMiniGameRoundAsync(MiniGame game, int result, int seat)
    {
        // Stat updates exactly as the reference: a give-up records only the loser's loss.
        game.AddResult(seat, result);
        if (result != MiniGame.ResultLose)
        {
            game.AddResult(seat == 1 ? 0 : 1, result == MiniGame.ResultWin ? MiniGame.ResultLose : MiniGame.ResultTie);
        }

        _characters.Save(game.Owner.Character);
        if (game.Visitor is { } visitor)
        {
            _characters.Save(visitor.Character);
        }

        await BroadcastToMiniGameAsync(game, _packets.MiniGameResult(game, result, seat)).ConfigureAwait(false);
        game.Open = true;
        game.RequestedTie = -1;
        await UpdateMiniGameBalloonAsync(game).ConfigureAwait(false);

        for (int s = MiniGame.MaxSize - 1; s >= 0; s--) // visitor first so the owner-close is last
        {
            if (game.ExitAfter[s] && game.PlayerAt(s) is { } player)
            {
                game.ExitAfter[s] = false;
                await ExitMiniGameAsync(game, player.Character.Id).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Handles the in-game mini-room ops (ports the MGRP_/ORP_/MGP_ cases of
    /// <c>ReqCMiniRoomBaseDlg.OnMiniRoom</c>): ready/start, Omok stones, match-card flips,
    /// tie/give-up/leave-after, turn timeouts, and kicks.
    /// </summary>
    private async ValueTask HandleMiniGameOpAsync(MapleSession session, Character c, byte protocol, PacketReader packet)
    {
        MiniGame? game = _miniGames.GetForCharacter(c.Id);
        if (game is null)
        {
            return;
        }

        int seat = game.SeatOf(c.Id);
        switch (protocol)
        {
            case ChannelPackets.MgReady:
            case ChannelPackets.MgCancelReady:
                if (seat == 1 && game.Open)
                {
                    game.Ready[1] = !game.Ready[1];
                    await BroadcastToMiniGameAsync(game, _packets.MiniGameReady(game.Ready[1])).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.MgStart:
                if (seat == 0 && game.Open && game.Visitor is not null && game.Ready[1])
                {
                    game.StartRound();
                    byte[] start = game.GameType == MiniGame.TypeOmok
                        ? _packets.MiniGameStart(game.Loser)
                        : _packets.MatchCardStart(game, game.Loser);
                    await BroadcastToMiniGameAsync(game, start).ConfigureAwait(false);
                    await UpdateMiniGameBalloonAsync(game).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.MgTieRequest:
                if (!game.Open)
                {
                    FieldPlayer? other = game.PlayerAt(seat == 0 ? 1 : 0);
                    if (other is not null)
                    {
                        await TrySendAsync(other, _packets.MiniGameTieRequest()).ConfigureAwait(false);
                    }

                    game.RequestedTie = seat;
                }

                break;

            case ChannelPackets.MgTieResult:
                if (!game.Open && game.RequestedTie > -1 && game.RequestedTie != seat)
                {
                    byte answer = packet.ReadByte();
                    if (answer > 0)
                    {
                        await EndMiniGameRoundAsync(game, MiniGame.ResultTie, game.RequestedTie).ConfigureAwait(false);
                        game.NextLoser();
                    }
                    else
                    {
                        await BroadcastToMiniGameAsync(game, _packets.MiniGameTieDenied()).ConfigureAwait(false);
                    }

                    game.RequestedTie = -1;
                }

                break;

            case ChannelPackets.MgGiveUpRequest:
                if (!game.Open)
                {
                    await EndMiniGameRoundAsync(game, MiniGame.ResultLose, seat).ConfigureAwait(false);
                    game.NextLoser();
                }

                break;

            case ChannelPackets.MgLeaveEngage:
            case ChannelPackets.MgLeaveEngageCancel:
                if (!game.Open && seat >= 0)
                {
                    game.ExitAfter[seat] = !game.ExitAfter[seat];
                    await BroadcastToMiniGameAsync(game, _packets.MiniGameExitAfter(game.ExitAfter[seat])).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.MgTimeOver:
                if (!game.Open)
                {
                    await BroadcastToMiniGameAsync(game, _packets.MiniGameSkip(seat)).ConfigureAwait(false);
                    game.NextLoser();
                }

                break;

            case ChannelPackets.MgBan:
                if (seat == 0 && game.Open && game.Visitor is { } banned)
                {
                    await TrySendAsync(banned, _packets.MiniRoomClosed(1, reason: 5)).ConfigureAwait(false);
                    _miniGames.RemoveVisitor(game);
                    game.Ready[1] = false;
                    await TrySendAsync(game.Owner, _packets.MiniRoomVisitorLeave(1)).ConfigureAwait(false);
                    await UpdateMiniGameBalloonAsync(game).ConfigureAwait(false);
                }

                break;

            case ChannelPackets.MgPutStone:
            {
                if (game.Open || game.GameType != MiniGame.TypeOmok)
                {
                    return;
                }

                int x = packet.ReadInt();
                int y = packet.ReadInt();
                byte type = packet.ReadByte();
                if (!game.TryPlacePiece(x, y, type))
                {
                    return; // occupied square — the reference silently ignores it
                }

                await BroadcastToMiniGameAsync(game, _packets.MiniGameOmokMove(x, y, type)).ConfigureAwait(false);
                if (game.HasFiveInARow(type))
                {
                    await EndMiniGameRoundAsync(game, MiniGame.ResultWin, seat).ConfigureAwait(false);
                }

                game.NextLoser(); // the reference advances the turn after every placement
                break;
            }

            case ChannelPackets.MgTurnUpCard:
            {
                if (game.Open || game.GameType != MiniGame.TypeMatchCard)
                {
                    return;
                }

                int slot = packet.ReadByte();
                int turn = game.Turn;
                int firstSlot = game.FirstSlot;
                FieldPlayer? other = game.PlayerAt(seat == 0 ? 1 : 0);

                if (turn == 1)
                {
                    // First card of the pair: echo it to the other seat only.
                    game.FirstSlot = slot;
                    if (other is not null)
                    {
                        await TrySendAsync(other, _packets.MatchCardSelect(turn, slot, firstSlot, turn)).ConfigureAwait(false);
                    }

                    game.Turn = 0;
                    return;
                }

                if (firstSlot > 0 && game.CardId(firstSlot + 1) == game.CardId(slot + 1))
                {
                    // Match: the flipper scores and keeps the turn.
                    await BroadcastToMiniGameAsync(game, _packets.MatchCardSelect(turn, slot, firstSlot, seat == 0 ? 2 : 3)).ConfigureAwait(false);
                    game.Points[seat]++;
                    if (game.Points[0] + game.Points[1] >= game.MatchesToWin)
                    {
                        bool tie = game.Points[0] == game.Points[1];
                        int winner = game.Points[1] > game.Points[0] ? 1 : 0;
                        await EndMiniGameRoundAsync(game, tie ? MiniGame.ResultTie : MiniGame.ResultWin, winner).ConfigureAwait(false);
                    }
                }
                else
                {
                    // Miss: the turn passes.
                    await BroadcastToMiniGameAsync(game, _packets.MatchCardSelect(turn, slot, firstSlot, seat == 0 ? 0 : 1)).ConfigureAwait(false);
                    game.NextLoser();
                }

                game.Turn = 1;
                game.FirstSlot = 0;
                break;
            }
        }
    }

    /// <summary>The Free Market rooms where personal shops may open (the reference's map gate).</summary>
    private static bool IsFreeMarketMap(int mapId) => mapId is >= 910000001 and <= 910000022;

    /// <summary>
    /// Sets up a personal shop (ports the MRP_Create shop branch): needs a store-permit cash item
    /// and a Free Market room. The shop opens in stocking mode; MRP_Balloon opens it for business.
    /// </summary>
    private async ValueTask CreatePlayerShopAsync(MapleSession session, Character c, PacketReader packet)
    {
        string description = packet.ReadString();
        packet.ReadByte();
        short slot = packet.ReadShort();
        int itemId = packet.ReadInt();

        if (!IsFreeMarketMap(c.MapId)
            || _playerShops.GetForCharacter(c.Id) is not null
            || _miniGames.GetForCharacter(c.Id) is not null
            || _trades.Get(c.Id) is not null)
        {
            return;
        }

        InventoryItem? permit = Inventory.ItemAt(c, Inventory.Tab(itemId), slot);
        if (permit is null || permit.ItemId != itemId)
        {
            return;
        }

        PlayerShop shop = _playerShops.Create(_player!, description, itemId);
        await session.SendAsync(_packets.PlayerShopRoom(shop, viewerSeat: 0)).ConfigureAwait(false);
    }

    /// <summary>MRP_Balloon — the owner finishes stocking and opens for business.</summary>
    private async ValueTask OpenPlayerShopForBusinessAsync(Character c)
    {
        if (_playerShops.GetForCharacter(c.Id) is { } shop && shop.SeatOf(c.Id) == 0 && !shop.Open)
        {
            shop.Open = true;
            await UpdatePlayerShopBalloonAsync(shop).ConfigureAwait(false);
        }
    }

    /// <summary>Sends a packet to everyone in the shop.</summary>
    private async ValueTask BroadcastToPlayerShopAsync(PlayerShop shop, byte[] packet, int exceptCharacterId = -1)
    {
        if (shop.Owner.Character.Id != exceptCharacterId)
        {
            await TrySendAsync(shop.Owner, packet).ConfigureAwait(false);
        }

        foreach (FieldPlayer? visitor in shop.Visitors)
        {
            if (visitor is not null && visitor.Character.Id != exceptCharacterId)
            {
                await TrySendAsync(visitor, packet).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask UpdatePlayerShopBalloonAsync(PlayerShop shop, bool closed = false)
    {
        Field field = _fields.Get(shop.Owner.Character.MapId);
        await field.BroadcastAsync(_packets.PlayerShopBalloon(shop.Owner.Character.Id, closed ? null : shop)).ConfigureAwait(false);
    }

    /// <summary>
    /// PSP_PutItem — the owner lists items for sale (ports the reference's checks): the stock
    /// leaves the inventory immediately; rechargeables list as one bundle of the whole stack.
    /// </summary>
    private async ValueTask HandleShopPutItemAsync(MapleSession session, Character c, PacketReader packet)
    {
        int tab = packet.ReadByte();
        short slot = packet.ReadShort();
        short bundles = packet.ReadShort();
        short perBundle = packet.ReadShort();
        int price = packet.ReadInt();

        // The stocking surface is shared: a personal shop or a managed hired merchant.
        List<PlayerShopItem>? listings = null;
        byte[]? refresh = null;
        if (_playerShops.GetForCharacter(c.Id) is { } shop && shop.SeatOf(c.Id) == 0)
        {
            listings = shop.Items;
        }
        else if (_merchants.GetForParticipant(c.Id) is { } merchant && merchant.SeatOf(c.Id) == 0)
        {
            listings = merchant.Items;
        }

        if (listings is null || price <= 0 || bundles <= 0 || perBundle <= 0)
        {
            return;
        }

        InventoryItem? item = Inventory.ItemAt(c, tab, slot);
        long total = (long)bundles * perBundle;
        if (item is null || total <= 0 || total > 32767)
        {
            return;
        }

        bool rechargeable = item.ItemId / 10000 is 207 or 233; // stars / bullets
        if (!rechargeable && item.Quantity < total)
        {
            return;
        }

        InventoryChange? change;
        if (rechargeable)
        {
            // The whole stack lists as a single bundle (ports the star/bullet special case).
            change = Inventory.RemoveFromSlot(c, tab, slot, item.Quantity);
            listings.Add(new PlayerShopItem(
                new InventoryItem { ItemId = item.ItemId, Quantity = item.Quantity }, bundles: 1, price));
        }
        else if (tab == 1)
        {
            // The equip instance itself goes on the shelf so its stats survive the sale.
            change = Inventory.RemoveFromSlot(c, tab, slot, 1);
            item.Quantity = 1;
            listings.Add(new PlayerShopItem(item, bundles: 1, price));
        }
        else
        {
            change = Inventory.RemoveFromSlot(c, tab, slot, (int)total);
            listings.Add(new PlayerShopItem(
                new InventoryItem { ItemId = item.ItemId, Quantity = perBundle }, bundles, price));
        }

        if (_playerShops.GetForCharacter(c.Id) is { } s2)
        {
            refresh = _packets.PlayerShopItemUpdate(s2);
        }
        else
        {
            HiredMerchant stockedMerchant = _merchants.GetForParticipant(c.Id)!;
            _merchants.Persist(stockedMerchant);
            refresh = _packets.HiredMerchantItemUpdate(stockedMerchant);
        }

        _characters.Save(c);
        if (change is { } ch)
        {
            await session.SendAsync(_packets.InventoryOperation(new[] { ch })).ConfigureAwait(false);
        }

        await session.SendAsync(refresh).ConfigureAwait(false);
    }

    /// <summary>PSP_BuyItem — a visitor buys bundles (ports <c>MaplePlayerShop.buy</c>).</summary>
    private async ValueTask HandleShopBuyItemAsync(MapleSession session, Character c, PacketReader packet)
    {
        int index = packet.ReadByte();
        short quantity = packet.ReadShort();

        PlayerShop? shop = _playerShops.GetForCharacter(c.Id);
        if (shop is null || shop.SeatOf(c.Id) <= 0 || index < 0 || index >= shop.Items.Count || quantity <= 0)
        {
            return;
        }

        PlayerShopItem listing = shop.Items[index];
        long units = (long)quantity * listing.Item.Quantity;
        long cost = (long)listing.Price * quantity;
        if (units <= 0 || units > 32767 || cost <= 0 || cost > int.MaxValue || c.Meso < cost)
        {
            return;
        }

        // Claim the bundles under the shop lock so two visitors can't oversell a listing.
        lock (shop.Items)
        {
            if (listing.Bundles < quantity)
            {
                return;
            }

            listing.Bundles -= quantity;
        }

        Character owner = shop.Owner.Character;

        // Hand the goods over: an equip carries its instance; bundles stack normally.
        List<InventoryChange> changes;
        if (Inventory.Tab(listing.Item.ItemId) == 1)
        {
            changes = new List<InventoryChange> { Inventory.Place(c, listing.Item) };
        }
        else
        {
            int slotMax = _items.GetConsume(listing.Item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax;
            changes = Inventory.Add(c, listing.Item.ItemId, (int)units, slotMax);
        }

        c.Meso -= (int)cost;
        owner.Meso = (int)Math.Clamp((long)owner.Meso + cost, 0, int.MaxValue);
        _characters.Save(c);
        _characters.Save(owner);

        if (changes.Count > 0)
        {
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await TrySendAsync(shop.Owner, _packets.StatChanged(owner, StatFlag.Meso)).ConfigureAwait(false);
        await BroadcastToPlayerShopAsync(shop, _packets.PlayerShopItemUpdate(shop)).ConfigureAwait(false);

        if (shop.IsSoldOut)
        {
            await ClosePlayerShopAsync(shop, PlayerShop.CloseReasonSoldOut).ConfigureAwait(false);
        }
    }

    /// <summary>PSP_MoveItemToInventory — the owner reclaims a listing.</summary>
    private async ValueTask HandleShopReclaimItemAsync(MapleSession session, Character c, PacketReader packet)
    {
        int index = packet.ReadShort();
        PlayerShop? shop = _playerShops.GetForCharacter(c.Id);
        if (shop is null || shop.SeatOf(c.Id) != 0 || index < 0 || index >= shop.Items.Count)
        {
            return;
        }

        PlayerShopItem listing = shop.Items[index];
        if (listing.Bundles > 0)
        {
            List<InventoryChange> changes = ReturnListingTo(c, listing);
            _characters.Save(c);
            if (changes.Count > 0)
            {
                await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
            }
        }

        shop.Items.RemoveAt(index);
        await session.SendAsync(_packets.PlayerShopItemUpdate(shop)).ConfigureAwait(false);
    }

    /// <summary>PSP_Ban — the owner throws a visitor out (ports <c>banPlayer</c>).</summary>
    private async ValueTask HandleShopBanAsync(Character c, PacketReader packet)
    {
        packet.ReadByte(); // claimed slot
        string name = packet.ReadString();
        PlayerShop? shop = _playerShops.GetForCharacter(c.Id);
        if (shop is null || shop.SeatOf(c.Id) != 0)
        {
            return;
        }

        for (int i = 0; i < shop.Visitors.Length; i++)
        {
            if (shop.Visitors[i] is { } visitor
                && string.Equals(visitor.Character.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                await TrySendAsync(visitor, _packets.MiniRoomClosed(1, PlayerShop.CloseReasonKicked)).ConfigureAwait(false);
                _playerShops.RemoveVisitor(shop, i + 1);
                await BroadcastToPlayerShopAsync(shop, _packets.MiniRoomVisitorLeave((byte)(i + 1))).ConfigureAwait(false);
                await UpdatePlayerShopBalloonAsync(shop).ConfigureAwait(false);
                return;
            }
        }
    }

    /// <summary>Puts a listing's remaining stock back into a character's inventory.</summary>
    private List<InventoryChange> ReturnListingTo(Character c, PlayerShopItem listing)
    {
        if (Inventory.Tab(listing.Item.ItemId) == 1)
        {
            listing.Bundles = 0;
            return new List<InventoryChange> { Inventory.Place(c, listing.Item) };
        }

        int units = listing.Bundles * listing.Item.Quantity;
        listing.Bundles = 0;
        int slotMax = _items.GetConsume(listing.Item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax;
        return Inventory.Add(c, listing.Item.ItemId, units, slotMax);
    }

    /// <summary>
    /// Sets up a hired merchant (ports the MRP_Create entrusted-shop branch): needs the employee
    /// permit cash item and a Free Market room, one merchant per owner. The owner enters the
    /// stocking view; MRP_Balloon then puts the employee NPC on the map.
    /// </summary>
    private async ValueTask CreateHiredMerchantAsync(MapleSession session, Character c, PacketReader packet)
    {
        string description = packet.ReadString();
        packet.ReadByte();
        short slot = packet.ReadShort();
        int itemId = packet.ReadInt();

        if (!IsFreeMarketMap(c.MapId)
            || _merchants.GetByOwner(c.Id) is not null
            || _merchants.GetForParticipant(c.Id) is not null
            || _playerShops.GetForCharacter(c.Id) is not null
            || _trades.Get(c.Id) is not null)
        {
            return;
        }

        InventoryItem? permit = Inventory.ItemAt(c, Inventory.Tab(itemId), slot);
        if (permit is null || permit.ItemId != itemId || itemId / 10000 != 503)
        {
            return;
        }

        HiredMerchant merchant = _merchants.Create(c, description, itemId, c.MapId, _player!.X, _player.Y, 0);
        _merchants.SetManager(merchant, _player);
        await session.SendAsync(_packets.HiredMerchantRoom(merchant, viewerSeat: 0, firstTime: true)).ConfigureAwait(false);
    }

    /// <summary>MRP_Balloon for a stocked merchant: the employee NPC goes live on the map and
    /// keeps selling with the owner gone (ports the MRP_Balloon merchant branch).</summary>
    private async ValueTask OpenHiredMerchantForBusinessAsync(HiredMerchant merchant)
    {
        _merchants.RemoveManager(merchant);
        merchant.Open = true;
        _merchants.Persist(merchant);
        Field field = _fields.Get(merchant.MapId);
        await field.BroadcastAsync(_packets.EmployeeEnterField(merchant)).ConfigureAwait(false);
    }

    /// <summary>Sends a packet to everyone inside the merchant room.</summary>
    private async ValueTask BroadcastToMerchantAsync(HiredMerchant merchant, byte[] packet, int exceptCharacterId = -1)
    {
        if (merchant.Manager is { } manager && manager.Character.Id != exceptCharacterId)
        {
            await TrySendAsync(manager, packet).ConfigureAwait(false);
        }

        foreach (FieldPlayer? visitor in merchant.Visitors)
        {
            if (visitor is not null && visitor.Character.Id != exceptCharacterId)
            {
                await TrySendAsync(visitor, packet).ConfigureAwait(false);
            }
        }
    }

    /// <summary>ESP_BuyItem — a visitor buys from the merchant (ports <c>HiredMerchant.buy</c>):
    /// the taxed price banks on the merchant, the sale lands in the owner's sold list.</summary>
    private async ValueTask HandleMerchantBuyItemAsync(MapleSession session, Character c, HiredMerchant merchant, PacketReader packet)
    {
        int index = packet.ReadByte();
        short quantity = packet.ReadShort();
        if (merchant.SeatOf(c.Id) <= 0 || index < 0 || index >= merchant.Items.Count || quantity <= 0)
        {
            return;
        }

        PlayerShopItem listing = merchant.Items[index];
        long units = (long)quantity * listing.Item.Quantity;
        long cost = (long)listing.Price * quantity;
        if (units <= 0 || units > 32767 || cost <= 0 || cost > int.MaxValue || c.Meso < cost)
        {
            return;
        }

        // Claim the bundles under the merchant lock so two shoppers can't oversell a listing.
        lock (merchant.Items)
        {
            if (listing.Bundles < quantity)
            {
                return;
            }

            listing.Bundles -= quantity;
        }

        List<InventoryChange> changes;
        if (Inventory.Tab(listing.Item.ItemId) == 1)
        {
            changes = new List<InventoryChange> { Inventory.Place(c, listing.Item) };
        }
        else
        {
            int slotMax = _items.GetConsume(listing.Item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax;
            changes = Inventory.Add(c, listing.Item.ItemId, (int)units, slotMax);
        }

        c.Meso -= (int)cost;
        merchant.Sold.Add(new SoldRecord(listing.Item.ItemId, quantity, (int)cost, c.Name));
        long banked = merchant.Meso + cost;
        merchant.Meso = (int)Math.Clamp(banked - HiredMerchant.Tax((int)Math.Min(banked, int.MaxValue)), 0, int.MaxValue);
        _characters.Save(c);

        if (changes.Count > 0)
        {
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        await BroadcastToMerchantAsync(merchant, _packets.HiredMerchantItemUpdate(merchant)).ConfigureAwait(false);
        _merchants.Persist(merchant);
    }

    /// <summary>ESP_MoveItemToInventory — the managing owner reclaims a listing.</summary>
    private async ValueTask HandleMerchantReclaimItemAsync(MapleSession session, Character c, HiredMerchant merchant, PacketReader packet)
    {
        int index = packet.ReadShort();
        if (merchant.SeatOf(c.Id) != 0 || index < 0 || index >= merchant.Items.Count)
        {
            return;
        }

        PlayerShopItem listing = merchant.Items[index];
        if (listing.Bundles > 0)
        {
            List<InventoryChange> changes = ReturnListingTo(c, listing);
            _characters.Save(c);
            if (changes.Count > 0)
            {
                await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
            }
        }

        merchant.Items.RemoveAt(index);
        _merchants.Persist(merchant);
        await session.SendAsync(_packets.HiredMerchantItemUpdate(merchant)).ConfigureAwait(false);
    }

    /// <summary>
    /// A participant leaves the merchant room. A visitor frees their seat; the managing owner
    /// leaving either reopens the store (stock remains) or packs it up — remaining stock and the
    /// banked (taxed) meso go back to the owner and the employee NPC leaves the map.
    /// </summary>
    private async ValueTask ExitHiredMerchantAsync(HiredMerchant merchant, int leavingCharacterId)
    {
        int seat = merchant.SeatOf(leavingCharacterId);
        if (seat > 0)
        {
            _merchants.RemoveVisitor(merchant, seat);
            await BroadcastToMerchantAsync(merchant, _packets.MiniRoomVisitorLeave((byte)seat)).ConfigureAwait(false);
            return;
        }

        if (seat != 0)
        {
            return;
        }

        _merchants.RemoveManager(merchant);
        if (merchant.Items.Any(i => i.Bundles > 0))
        {
            // Stock remains: back to business.
            merchant.Open = true;
            _merchants.Persist(merchant);
            Field field = _fields.Get(merchant.MapId);
            await field.BroadcastAsync(_packets.EmployeeMiniRoomBalloon(merchant)).ConfigureAwait(false);
            return;
        }

        await CloseHiredMerchantAsync(merchant).ConfigureAwait(false);
    }

    /// <summary>Packs the merchant up: stock + banked meso return to the owner, the NPC leaves.</summary>
    private async ValueTask CloseHiredMerchantAsync(HiredMerchant merchant)
    {
        foreach (FieldPlayer? visitor in merchant.Visitors)
        {
            if (visitor is not null)
            {
                await TrySendAsync(visitor, _packets.MiniRoomClosed(1, PlayerShop.CloseReasonClosed)).ConfigureAwait(false);
            }
        }

        Character? owner = _characters.Find(merchant.OwnerId);
        if (owner is not null)
        {
            var returned = new List<InventoryChange>();
            foreach (PlayerShopItem listing in merchant.Items)
            {
                if (listing.Bundles > 0)
                {
                    returned.AddRange(ReturnListingTo(owner, listing));
                }
            }

            owner.Meso = (int)Math.Clamp((long)owner.Meso + merchant.Meso, 0, int.MaxValue);
            _characters.Save(owner);

            if (FindOnlinePlayer(owner.Id) is { } online)
            {
                if (returned.Count > 0)
                {
                    await TrySendAsync(online, _packets.InventoryOperation(returned)).ConfigureAwait(false);
                }

                await TrySendAsync(online, _packets.StatChanged(owner, StatFlag.Meso)).ConfigureAwait(false);
            }
        }

        _merchants.Remove(merchant);
        Field field = _fields.Get(merchant.MapId);
        await field.BroadcastAsync(_packets.EmployeeLeaveField(merchant)).ConfigureAwait(false);
    }

    /// <summary>A participant leaves the shop; the owner leaving (or a sell-out) closes it,
    /// returning unsold stock (ports <c>MaplePlayerShop.closeShop</c> / <c>removeVisitor</c>).</summary>
    private async ValueTask ExitPlayerShopAsync(PlayerShop shop, int leavingCharacterId)
    {
        int seat = shop.SeatOf(leavingCharacterId);
        if (seat == 0)
        {
            await ClosePlayerShopAsync(shop, PlayerShop.CloseReasonClosed).ConfigureAwait(false);
        }
        else if (seat > 0)
        {
            _playerShops.RemoveVisitor(shop, seat);
            await BroadcastToPlayerShopAsync(shop, _packets.MiniRoomVisitorLeave((byte)seat)).ConfigureAwait(false);
            await UpdatePlayerShopBalloonAsync(shop).ConfigureAwait(false);
        }
    }

    private async ValueTask ClosePlayerShopAsync(PlayerShop shop, byte reason)
    {
        // Visitors are shown the door first, then unsold stock returns to the owner.
        foreach (FieldPlayer? visitor in shop.Visitors)
        {
            if (visitor is not null)
            {
                await TrySendAsync(visitor, _packets.MiniRoomClosed(1, reason)).ConfigureAwait(false);
            }
        }

        Character owner = shop.Owner.Character;
        var returned = new List<InventoryChange>();
        foreach (PlayerShopItem listing in shop.Items)
        {
            if (listing.Bundles > 0)
            {
                returned.AddRange(ReturnListingTo(owner, listing));
            }
        }

        _characters.Save(owner);
        if (returned.Count > 0)
        {
            await TrySendAsync(shop.Owner, _packets.InventoryOperation(returned)).ConfigureAwait(false);
        }

        await TrySendAsync(shop.Owner, _packets.MiniRoomClosed(0, reason)).ConfigureAwait(false);
        _playerShops.Remove(shop);
        await UpdatePlayerShopBalloonAsync(shop, closed: true).ConfigureAwait(false);
    }

    // CP_FriendRequest flags (OpsFriend).
    private const byte FriendReqLoad = 0;
    private const byte FriendReqSet = 1;
    private const byte FriendReqAccept = 2;
    private const byte FriendReqDelete = 3;


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

                if (c.Buddies.Count >= c.BuddyCapacity)
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

                if (t.Buddies.Count >= t.BuddyCapacity)
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

    // CP_GuildRequest ops (the reference GuildHandler's raw switch values).
    private const byte GuildReqCreate = 0x02;
    private const byte GuildReqInvite = 0x05;
    private const byte GuildReqJoin = 0x06;
    private const byte GuildReqLeave = 0x07;
    private const byte GuildReqExpel = 0x08;
    private const byte GuildReqRankTitles = 0x0D;
    private const byte GuildReqRankChange = 0x0E;
    private const byte GuildReqEmblem = 0x0F;
    private const byte GuildReqNotice = 0x10;

    /// <summary>The Orbis guild headquarters map, where creation/emblem changes happen.</summary>
    private const int GuildHqMapId = 200000301;
    private const int GuildCreateCost = 5_000_000;
    private const int GuildEmblemCost = 15_000_000;

    /// <summary>
    /// Handles <c>CP_GuildRequest</c> — the guild window (ports <c>GuildHandler.Guild</c>):
    /// create (at the HQ, for meso), invite/join/leave/expel, rank titles and ranks, emblem, and
    /// notice. The leader leaving disbands the guild (same simplification as party leadership).
    /// </summary>
    private async ValueTask HandleGuildRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        byte op = packet.ReadByte();
        switch (op)
        {
            case GuildReqCreate:
            {
                string name = packet.ReadString();
                if (c.MapId != GuildHqMapId)
                {
                    await session.SendAsync(_packets.BroadcastNotice("ギルドはギルド本部でのみ作成できます。", alert: true)).ConfigureAwait(false);
                    return;
                }

                await CreateGuildAsync(session, c, name, cost: GuildCreateCost).ConfigureAwait(false);
                break;
            }

            case GuildReqInvite:
            {
                if (c.GuildId <= 0 || c.GuildRank > 2) // 1 = master, 2 = jr. master
                {
                    return;
                }

                string name = packet.ReadString();
                FieldPlayer? target = FindOnlinePlayerByName(name);
                if (target is null)
                {
                    await session.SendAsync(_packets.GuildMessage(ChannelPackets.GuildResTargetOffline)).ConfigureAwait(false);
                }
                else if (target.Character.GuildId > 0)
                {
                    await session.SendAsync(_packets.GuildMessage(ChannelPackets.GuildResTargetInGuild)).ConfigureAwait(false);
                }
                else
                {
                    _guilds.Invite(target.Character.Name, c.GuildId);
                    await TrySendAsync(target, _packets.GuildInvite(c.GuildId, c.Name, c.Level, c.Job)).ConfigureAwait(false);
                }

                break;
            }

            case GuildReqJoin:
            {
                int guildId = packet.ReadInt();
                int characterId = packet.ReadInt();
                if (characterId != c.Id || c.GuildId > 0 || !_guilds.TakeInvite(c.Name, guildId))
                {
                    return;
                }

                GuildData? guild = _guilds.Get(guildId);
                if (guild is null)
                {
                    return;
                }

                IReadOnlyList<Character> members = _characters.ListByGuild(guildId);
                if (members.Count >= guild.Capacity)
                {
                    await session.SendAsync(_packets.BroadcastNotice("そのギルドは満員です。", alert: true)).ConfigureAwait(false);
                    return;
                }

                c.GuildId = guildId;
                c.GuildRank = 5;
                _characters.Save(c);
                _guilds.SetOnline(guildId, _player);

                var row = new ChannelPackets.GuildMemberRow(c.Id, c.Name, c.Job, c.Level, c.GuildRank, Online: true);
                await BroadcastToGuildAsync(guildId, _packets.GuildNewMember(guildId, row)).ConfigureAwait(false);
                await session.SendAsync(_packets.GuildInfo(guild, BuildGuildMembers(guildId))).ConfigureAwait(false);
                break;
            }

            case GuildReqLeave:
            {
                int characterId = packet.ReadInt();
                string name = packet.ReadString();
                if (characterId != c.Id || !string.Equals(name, c.Name, StringComparison.Ordinal) || c.GuildId <= 0)
                {
                    return;
                }

                if (_guilds.Get(c.GuildId) is { } guild && guild.LeaderId == c.Id)
                {
                    await DisbandGuildAsync(guild).ConfigureAwait(false);
                }
                else
                {
                    int guildId = c.GuildId;
                    await BroadcastToGuildAsync(guildId, _packets.GuildMemberLeft(guildId, c.Id, c.Name, expelled: false)).ConfigureAwait(false);
                    c.GuildId = 0;
                    c.GuildRank = 0;
                    _characters.Save(c);
                    _guilds.SetOffline(guildId, c.Id);
                    await session.SendAsync(_packets.GuildInfoNone()).ConfigureAwait(false);
                }

                break;
            }

            case GuildReqExpel:
            {
                int characterId = packet.ReadInt();
                packet.ReadString(); // the claimed name; the server uses the repo's record
                if (c.GuildId <= 0 || c.GuildRank > 2)
                {
                    return;
                }

                Character? target = _characters.Find(characterId);
                if (target is null || target.GuildId != c.GuildId || target.Id == c.Id)
                {
                    return;
                }

                int guildId = c.GuildId;
                await BroadcastToGuildAsync(guildId, _packets.GuildMemberLeft(guildId, target.Id, target.Name, expelled: true)).ConfigureAwait(false);
                target.GuildId = 0;
                target.GuildRank = 0;
                _characters.Save(target);
                if (FindOnlinePlayer(target.Id) is { } online)
                {
                    await TrySendAsync(online, _packets.GuildInfoNone()).ConfigureAwait(false);
                }

                _guilds.SetOffline(guildId, target.Id);
                break;
            }

            case GuildReqRankTitles:
            {
                if (_guilds.Get(c.GuildId) is not { } guild || guild.LeaderId != c.Id)
                {
                    return;
                }

                var titles = new List<string>(5);
                for (int i = 0; i < 5; i++)
                {
                    titles.Add(packet.ReadString());
                }

                guild.RankTitles = titles;
                _guilds.Save(guild);
                await BroadcastToGuildAsync(guild.Id, _packets.GuildRankTitles(guild.Id, titles)).ConfigureAwait(false);
                break;
            }

            case GuildReqRankChange:
            {
                int characterId = packet.ReadInt();
                byte newRank = packet.ReadByte();

                // Ports the reference gates: only 2..5 assignable, jr+ may demote/promote, and
                // ranks 2 and below are the master's alone to grant.
                if (newRank is <= 1 or > 5 || c.GuildRank > 2 || (newRank <= 2 && c.GuildRank != 1) || c.GuildId <= 0)
                {
                    return;
                }

                Character? target = _characters.Find(characterId);
                if (target is null || target.GuildId != c.GuildId)
                {
                    return;
                }

                target.GuildRank = newRank;
                _characters.Save(target);
                await BroadcastToGuildAsync(c.GuildId, _packets.GuildMemberRankChanged(c.GuildId, target.Id, newRank)).ConfigureAwait(false);
                break;
            }

            case GuildReqEmblem:
            {
                if (_guilds.Get(c.GuildId) is not { } guild || guild.LeaderId != c.Id || c.MapId != GuildHqMapId)
                {
                    return;
                }

                if (c.Meso < GuildEmblemCost)
                {
                    await session.SendAsync(_packets.BroadcastNotice("メルが足りません。", alert: true)).ConfigureAwait(false);
                    return;
                }

                guild.LogoBG = packet.ReadShort();
                guild.LogoBGColor = packet.ReadByte();
                guild.Logo = packet.ReadShort();
                guild.LogoColor = packet.ReadByte();
                _guilds.Save(guild);

                c.Meso -= GuildEmblemCost;
                _characters.Save(c);
                await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
                await BroadcastToGuildAsync(guild.Id, _packets.GuildEmblemChanged(guild.Id, guild.LogoBG, guild.LogoBGColor, guild.Logo, guild.LogoColor)).ConfigureAwait(false);
                break;
            }

            case GuildReqNotice:
            {
                string notice = packet.ReadString();
                if (notice.Length > 100 || c.GuildId <= 0 || c.GuildRank > 2)
                {
                    return;
                }

                if (_guilds.Get(c.GuildId) is not { } guild)
                {
                    return;
                }

                guild.Notice = notice;
                _guilds.Save(guild);
                await BroadcastToGuildAsync(guild.Id, _packets.GuildNotice(guild.Id, notice)).ConfigureAwait(false);
                break;
            }
        }
    }

    /// <summary>
    /// Creates a guild with this player as leader (rank 1); <paramref name="cost"/> is deducted
    /// (0 for the free <c>/guildcreate</c> command). Shared by the client's HQ flow and the command.
    /// </summary>
    private async ValueTask CreateGuildAsync(MapleSession session, Character c, string name, int cost)
    {
        if (c.GuildId > 0 || name.Length is < 1 or > 12)
        {
            return;
        }

        if (_guilds.FindByName(name) is not null)
        {
            await session.SendAsync(_packets.GuildMessage(ChannelPackets.GuildResNameInUse)).ConfigureAwait(false);
            return;
        }

        if (cost > 0 && c.Meso < cost)
        {
            await session.SendAsync(_packets.BroadcastNotice("メルが足りません。", alert: true)).ConfigureAwait(false);
            return;
        }

        GuildData guild = _guilds.Create(name, c.Id);
        c.GuildId = guild.Id;
        c.GuildRank = 1;
        if (cost > 0)
        {
            c.Meso -= cost;
        }

        _characters.Save(c);
        _guilds.SetOnline(guild.Id, _player!);

        if (cost > 0)
        {
            await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.GuildInfo(guild, BuildGuildMembers(guild.Id))).ConfigureAwait(false);
    }

    /// <summary>Disbands a guild: every member (online or not) becomes guildless.</summary>
    private async ValueTask DisbandGuildAsync(GuildData guild)
    {
        // Mutate state first so a member reacting to the packet can't observe the old guild.
        IReadOnlyCollection<FieldPlayer> online = _guilds.OnlineMembers(guild.Id);
        foreach (Character member in _characters.ListByGuild(guild.Id))
        {
            member.GuildId = 0;
            member.GuildRank = 0;
            _characters.Save(member);
        }

        _guilds.Delete(guild.Id); // also clears the online roster

        foreach (FieldPlayer member in online)
        {
            await TrySendAsync(member, _packets.GuildDisband(guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_GuildResult</c> — declining a guild invitation (ports
    /// <c>GuildHandler.DenyGuildRequest</c>): the original inviter is told who declined.
    /// </summary>
    private async ValueTask HandleGuildDenyAsync(PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        packet.ReadByte(); // mode
        string inviterName = packet.ReadString();
        if (FindOnlinePlayerByName(inviterName) is { } inviter)
        {
            await TrySendAsync(inviter, _packets.GuildInviteDenied(_player.Character.Name)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_GroupMessage</c> — friend / party / guild chat (ports
    /// <c>ReqCUser.OnGroupMessage</c>): relays the line to the group's other online members via
    /// <c>LP_GroupMessage</c>. Friend chat targets the ids the client listed (gated on the buddy
    /// list); party and guild membership come from the server's own registries.
    /// </summary>
    private async ValueTask HandleGroupMessageAsync(PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        Character c = _player.Character;
        byte chatTarget = packet.ReadByte();
        int memberCount = packet.ReadByte();
        var memberIds = new int[memberCount];
        for (int i = 0; i < memberCount; i++)
        {
            memberIds[i] = packet.ReadInt();
        }

        string text = packet.ReadString();

        switch (chatTarget)
        {
            case ChannelPackets.ChatGroupFriend:
                foreach (int id in memberIds)
                {
                    if (id != c.Id
                        && FindOnlinePlayer(id) is { } friend
                        && friend.Character.Buddies.TryGetValue(c.Id, out BuddyEntry? entry)
                        && !entry.Hidden)
                    {
                        await TrySendAsync(friend, _packets.GroupMessage(ChannelPackets.ChatGroupFriend, c.Name, text)).ConfigureAwait(false);
                    }
                }

                break;

            case ChannelPackets.ChatGroupParty:
                if (_parties.GetForCharacter(c.Id) is { } party)
                {
                    foreach (FieldPlayer member in party.Members)
                    {
                        if (member.Character.Id != c.Id)
                        {
                            await TrySendAsync(member, _packets.GroupMessage(ChannelPackets.ChatGroupParty, c.Name, text)).ConfigureAwait(false);
                        }
                    }
                }

                break;

            case ChannelPackets.ChatGroupGuild:
                if (c.GuildId > 0)
                {
                    await BroadcastToGuildAsync(c.GuildId, _packets.GroupMessage(ChannelPackets.ChatGroupGuild, c.Name, text), exceptCharacterId: c.Id).ConfigureAwait(false);
                }

                break;
        }
    }

    /// <summary>The wire member table for a guild, derived from the character store.</summary>
    private List<ChannelPackets.GuildMemberRow> BuildGuildMembers(int guildId)
    {
        var rows = new List<ChannelPackets.GuildMemberRow>();
        foreach (Character m in _characters.ListByGuild(guildId))
        {
            rows.Add(new ChannelPackets.GuildMemberRow(
                m.Id, m.Name, m.Job, m.Level, m.GuildRank, FindOnlinePlayer(m.Id) is not null));
        }

        return rows;
    }

    /// <summary>Sends a packet to every online guild member (optionally excluding one).</summary>
    private async ValueTask BroadcastToGuildAsync(int guildId, byte[] packet, int exceptCharacterId = -1)
    {
        foreach (FieldPlayer member in _guilds.OnlineMembers(guildId))
        {
            if (member.Character.Id != exceptCharacterId)
            {
                await TrySendAsync(member, packet).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Handles <c>CP_UserGatherItemRequest</c> — the inventory "gather" button (ports
    /// <c>OnUserGatherItemRequest</c>): compacts the tab and relays the moves + the ack.
    /// </summary>
    private async ValueTask HandleGatherItemAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        packet.ReadInt(); // timestamp
        byte tab = packet.ReadByte();
        List<InventoryChange> changes = Inventory.Gather(_player.Character, tab);
        if (changes.Count > 0)
        {
            _characters.Save(_player.Character);
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.GatherItemResult(tab)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserSortItemRequest</c> — the inventory "sort" button (ports
    /// <c>OnUserSortItemRequest</c>): selection-sorts the tab by item id and relays the swap moves.
    /// </summary>
    private async ValueTask HandleSortItemAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        packet.ReadInt(); // timestamp
        byte tab = packet.ReadByte();
        List<InventoryChange> changes = Inventory.Sort(_player.Character, tab);
        if (changes.Count > 0)
        {
            _characters.Save(_player.Character);
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        await session.SendAsync(_packets.SortItemResult(tab)).ConfigureAwait(false);
    }

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
        byte[] joinPacket = _packets.PartyJoin(target.Id, _player.Character.Name, target.ViewSlots(PartyChannel), target.LeaderId, PartyChannel);
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

            case "snotice" when parts.Length >= 2:
            {
                byte[] notice = _packets.BroadcastNotice(command["snotice ".Length..].Trim());
                foreach (Field f in _fields.Fields)
                {
                    await f.BroadcastAsync(notice).ConfigureAwait(false);
                }

                break;
            }

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
                int target = Math.Clamp(level, 1, 200);
                StatFlag levelChanged = StatFlag.Level | StatFlag.Exp;
                if (target > lc.Level)
                {
                    // Raising runs real level-ups so HP/MP/AP/SP grow like normal play.
                    levelChanged |= CharacterProgression.ForceLevelUps(lc, target - lc.Level, EffectResolverFor(lc));
                }
                else
                {
                    lc.Level = (byte)target; // lowering just sets the level (stats keep their values)
                }

                lc.Exp = 0; // reset so the new level's bar starts clean
                _characters.Save(lc);
                await session.SendAsync(_packets.StatChanged(lc, levelChanged)).ConfigureAwait(false);
                await RefreshPartyWindowAsync(_player).ConfigureAwait(false); // party window shows levels
                if (lc.GuildId > 0)
                {
                    await BroadcastToGuildAsync(lc.GuildId, _packets.GuildMemberLevelJob(lc.GuildId, lc.Id, lc.Level, lc.Job), exceptCharacterId: lc.Id).ConfigureAwait(false);
                }

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

            case "maxskills":
            {
                Character sc = _player!.Character;
                int learned = 0;
                foreach (int jobFile in JobSkillBooks(sc.Job))
                {
                    foreach (int skillId in _skills.GetSkillIds(jobFile))
                    {
                        int max = _skills.GetMaxLevel(skillId);
                        if (max > 0)
                        {
                            sc.Skills[skillId] = max;
                            await session.SendAsync(_packets.ChangeSkillRecordResult(skillId, max)).ConfigureAwait(false);
                            learned++;
                        }
                    }
                }

                _characters.Save(sc);
                await ReplyAsync(session, $"maxed {learned} skills for job {sc.Job}").ConfigureAwait(false);
                break;
            }

            case "guildcreate" when parts.Length >= 2:
                // Free, works anywhere (the client's own flow needs the HQ map and 5m meso).
                await CreateGuildAsync(session, _player!.Character, parts[1], cost: 0).ConfigureAwait(false);
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
                    + "/item <id> [qty], /shop <id>, /storage, /guildcreate <name>, /maxskills, /save, /players, /notice <msg>, /snotice <msg>, /pos, /help")
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

    /// <summary>
    /// The skill-book file ids a job can learn from: the beginner book, the 1st-job book, then
    /// each advancement up to the current code (e.g. 112 → 000, 100, 110, 111, 112).
    /// </summary>
    private static IEnumerable<int> JobSkillBooks(int job)
    {
        yield return 0; // beginner skills
        if (job <= 0)
        {
            yield break;
        }

        int first = job / 100 * 100;
        yield return first;
        if (job == first)
        {
            yield break;
        }

        for (int j = job / 10 * 10; j <= job; j++)
        {
            yield return j;
        }
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
        if (_conversation is null)
        {
            // No script: a friendly one-liner (with the NPC's real name when String data is
            // loaded) so every NPC at least responds. The client's dialog-close answer is
            // ignored by HandleScriptAnswer since no conversation is active.
            string? npcName = _npcNames?.GetName(templateId);
            string line = npcName is null
                ? "……。"
                : $"こんにちは、{npcName}です。今は特にお話しすることがありません。";
            dialog.Say(templateId, line, prev: false, next: false);
        }
    }

    /// <summary>The <c>player</c> object handed to NPC / quest / portal scripts.</summary>
    private ChannelPlayer CreateScriptPlayer(MapleSession session) => new(
        _player!.Character, _characters, session, _packets,
        warp: (map, portal) => MovePlayerToMapAsync(session, map, portal),
        openShop: shopId => _shops.GetShop(shopId) is { } s ? OpenShopAsync(session, s) : ValueTask.CompletedTask,
        openStorage: () => OpenStorageAsync(session),
        gainItem: (itemId, quantity) => ScriptGainItemAsync(session, itemId, quantity),
        itemCount: itemId => CountInventoryItem(_player!.Character, itemId),
        effectOf: EffectResolverFor(_player!.Character),
        styles: _styles,
        avatarModified: () => _field is { } f
            ? f.BroadcastAsync(_packets.UserAvatarModified(_player!.Character), exceptCharacterId: _player!.Character.Id)
            : ValueTask.CompletedTask,
        hasMerchant: () => _merchants.GetByOwner(_player!.Character.Id) is not null,
        retrieveMerchant: RetrieveMerchantAsync);

    /// <summary>
    /// Packs up the player's hired merchant from afar (the Fredrick service): visitors are shown
    /// out, unsold stock and banked meso return to the owner. False when they have none.
    /// </summary>
    private async ValueTask<bool> RetrieveMerchantAsync()
    {
        if (_player is null || _merchants.GetByOwner(_player.Character.Id) is not { } merchant)
        {
            return false;
        }

        await CloseHiredMerchantAsync(merchant).ConfigureAwait(false);
        return true;
    }

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
            await session.SendAsync(_packets.UserEnterField(other, GuildOf(other.Character))).ConfigureAwait(false);
        }

        newField.Enter(player);
        _field = newField;
        await newField.BroadcastAsync(_packets.UserEnterField(player, GuildOf(player.Character)), exceptCharacterId: player.Character.Id)
            .ConfigureAwait(false);

        await SpawnReactorsAsync(session, newField).ConfigureAwait(false);

        // The pet follows its owner through the portal (ports the transfer-field respawn).
        if (player.Pet is { } pet)
        {
            pet.X = player.X;
            pet.Y = player.Y;
            await newField.BroadcastAsync(_packets.PetActivated(player.Character.Id, pet, transferField: true)).ConfigureAwait(false);
        }

        // Open game rooms and shops in the new map show their balloons.
        foreach (MiniGame game in _miniGames.GamesInMap(targetMapId))
        {
            await session.SendAsync(_packets.MiniRoomBalloon(game.Owner.Character.Id, game)).ConfigureAwait(false);
        }

        foreach (PlayerShop shop in _playerShops.ShopsInMap(targetMapId))
        {
            await session.SendAsync(_packets.PlayerShopBalloon(shop.Owner.Character.Id, shop)).ConfigureAwait(false);
        }

        foreach (HiredMerchant merchant in _merchants.MerchantsInMap(targetMapId))
        {
            await session.SendAsync(_packets.EmployeeEnterField(merchant)).ConfigureAwait(false);
        }

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
