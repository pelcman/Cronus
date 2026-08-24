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
public sealed partial class ChannelHandler : PacketHandlerBase
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
    private readonly IAccountRepository? _accounts;
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
    private readonly IReactorDropProvider _reactorDrops;
    private readonly PortalScriptEngine? _reactorScripts;
    private readonly INpcNameProvider? _npcNames;

    /// <summary>Valid hair/face/skin ids from game data, for salon scripts; null without wz.</summary>
    private readonly IStyleProvider? _styles;

    /// <summary>Every known item grouped by category (for /dbgshop); null without wz.</summary>
    private readonly IItemCatalog? _itemCatalog;

    /// <summary>Every named map grouped by region (for /dbgwarp); null without wz.</summary>
    private readonly IMapCatalog? _mapCatalog;

    /// <summary>Which NPCs have quests (their clicks stay silent for the client's quest UI).</summary>
    private readonly IQuestNpcIndex? _questNpcs;

    /// <summary>Every channel's advertised endpoint (index = channel id); null = single channel.</summary>
    private readonly IReadOnlyList<System.Net.IPEndPoint>? _channelEndpoints;

    /// <summary>Every channel's field registry (index = channel id) for cross-channel lookups.</summary>
    private readonly IReadOnlyList<FieldRegistry>? _worldFields;

    /// <summary>The cash-shop server's advertised endpoint; null = no cash shop (decline).</summary>
    private readonly System.Net.IPEndPoint? _cashShopEndpoint;
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
    private readonly int _opPetDropPickUp;
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
    private readonly int _opNpcMove;
    private readonly int _opSummonedMove;
    private readonly int _opSummonedAttack;
    private readonly int _opSummonedHit;
    private readonly int _opEnterTownPortal;
    private readonly int _opRpsGame;
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
        IReactorDropProvider? reactorDrops = null,
        PortalScriptEngine? reactorScripts = null,
        INpcNameProvider? npcNames = null,
        IStyleProvider? styles = null,
        IItemCatalog? itemCatalog = null,
        IMapCatalog? mapCatalog = null,
        IQuestNpcIndex? questNpcs = null,
        IReadOnlyList<System.Net.IPEndPoint>? channelEndpoints = null,
        IReadOnlyList<FieldRegistry>? worldFields = null,
        System.Net.IPEndPoint? cashShopEndpoint = null,
        IAccountRepository? accounts = null)
    {
        _accounts = accounts;
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
        _reactorDrops = reactorDrops ?? new InMemoryReactorDropProvider(new Dictionary<int, IReadOnlyList<ReactorDropEntry>>());
        _reactorScripts = reactorScripts;
        _npcNames = npcNames;
        _styles = styles;
        _itemCatalog = itemCatalog;
        _mapCatalog = mapCatalog;
        _questNpcs = questNpcs;
        _channelEndpoints = channelEndpoints;
        _worldFields = worldFields;
        _cashShopEndpoint = cashShopEndpoint;
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
        _opPetDropPickUp = clientOpcodes.Get(ClientOpcode.PetDropPickUpRequest);
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
        _opNpcMove = clientOpcodes.Get(ClientOpcode.NpcMove);
        _opSummonedMove = clientOpcodes.Get(ClientOpcode.SummonedMove);
        _opSummonedAttack = clientOpcodes.Get(ClientOpcode.SummonedAttack);
        _opSummonedHit = clientOpcodes.Get(ClientOpcode.SummonedHit);
        _opEnterTownPortal = clientOpcodes.Get(ClientOpcode.EnterTownPortalRequest);
        _opRpsGame = clientOpcodes.Get(ClientOpcode.RpsGame);
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
            await HandleTransferChannelAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMigrateCashShop)
        {
            await HandleMigrateCashShopAsync(session).ConfigureAwait(false);
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
        else if (opcode == _opPetDropPickUp)
        {
            await HandlePetDropPickUpAsync(session, packet).ConfigureAwait(false);
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
        else if (opcode == _opNpcMove)
        {
            await HandleNpcMoveAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMobMove)
        {
            await HandleMobMoveAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opSummonedMove)
        {
            await HandleSummonedMoveAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opSummonedAttack)
        {
            await HandleSummonedAttackAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opSummonedHit)
        {
            await HandleSummonedHitAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opEnterTownPortal)
        {
            await HandleEnterTownPortalAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opRpsGame)
        {
            await HandleRpsGameAsync(session, packet).ConfigureAwait(false);
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

            // The party survives the drop (relog / channel switch re-attaches); the member just
            // shows offline. When the last member drops, the party dissolves.
            Party? party = _parties.GetForCharacter(_player.Character.Id);
            if (party is not null)
            {
                await PartyMemberWentOfflineAsync(party, _player.Character.Id).ConfigureAwait(false);
            }

            // Their summons and door vanish with them.
            foreach (FieldSummon summon in _field.RemoveSummonsOf(_player.Character.Id))
            {
                await _field.BroadcastAsync(_packets.SummonedLeaveField(summon, animated: false)).ConfigureAwait(false);
            }

            await RemoveDoorOfAsync(_player.Character.Id).ConfigureAwait(false);

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

        var player = new FieldPlayer(character, session) { Channel = _channelId };
        character.LastChannel = _channelId; // the cash shop sends the client back here
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
        await session.SendAsync(_packets.FamilyPrivilegeList()).ConfigureAwait(false); // before info (reference order)
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

        await NotifyBuddiesOfPresenceAsync(character.Id, _channelId).ConfigureAwait(false); // "came online"

        // Rejoining a surviving party (relog / channel switch) re-attaches the live presence
        // and refreshes every member's window.
        if (_parties.GetForCharacter(character.Id) is { } rejoined && rejoined.Reattach(player))
        {
            byte[] refresh = _packets.PartyRefresh(rejoined.Id, rejoined.ViewSlots(), rejoined.LeaderId, PartyChannel, loading: false);
            foreach (FieldPlayer member in rejoined.Members)
            {
                await TrySendAsync(member, refresh).ConfigureAwait(false);
            }

            await SyncPartyHpAsync(rejoined, player).ConfigureAwait(false);
        }

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

        // Standing summons appear in place; assist summons show at their owner (they follow).
        foreach (FieldSummon summon in field.Summons)
        {
            if (!summon.IsPuppet
                && field.Players.FirstOrDefault(p => p.Character.Id == summon.OwnerId) is { } owner)
            {
                summon.X = owner.X;
                summon.Y = owner.Y;
            }

            await session.SendAsync(_packets.SummonedEnterField(summon, animated: true)).ConfigureAwait(false);
        }

        // Standing Mystic Door sides in this map.
        foreach (MysticDoor door in field.Doors)
        {
            (short x, short y) = door.PositionIn(field.MapId);
            await session.SendAsync(
                _packets.TownPortalCreated(door.OwnerId, x, y, isTown: door.IsTownSide(field.MapId))).ConfigureAwait(false);
        }
    }

}
