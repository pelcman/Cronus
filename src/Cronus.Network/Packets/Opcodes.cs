namespace Cronus.Network.Packets;

/// <summary>
/// Well-known inbound (client → server) opcode <b>names</b>. Resolve to numeric values via
/// <see cref="OpcodeTable"/>. Names mirror upstream <c>ClientPacketHeader</c>; only the ones
/// referenced by handlers are listed here — the full set lives in the .properties data.
/// </summary>
public static class ClientOpcode
{
    public const string CheckPassword = "CP_CheckPassword";
    public const string WorldInfoRequest = "CP_WorldInfoRequest";
    public const string SelectWorld = "CP_SelectWorld";
    public const string LogoutWorld = "CP_LogoutWorld";
    public const string SelectCharacter = "CP_SelectCharacter";
    public const string MigrateIn = "CP_MigrateIn";
    public const string CheckDuplicatedId = "CP_CheckDuplicatedID";
    public const string ViewAllChar = "CP_ViewAllChar";
    public const string CreateNewCharacter = "CP_CreateNewCharacter";
    public const string DeleteCharacter = "CP_DeleteCharacter";
    public const string AliveAck = "CP_AliveAck";
    public const string SecurityPacket = "CP_SecurityPacket";
    public const string JmsMapLogin = "CP_JMS_MapLogin";
    public const string JmsSafetyPassword = "CP_JMS_SafetyPassword";
    public const string JmsGetMapLogin = "CP_JMS_GetMapLogin";
    public const string UserMove = "CP_UserMove";
    public const string UserChat = "CP_UserChat";
    public const string UserEmotion = "CP_UserEmotion";
    public const string UserSitRequest = "CP_UserSitRequest";
    public const string UserMeleeAttack = "CP_UserMeleeAttack";
    public const string UserShootAttack = "CP_UserShootAttack";
    public const string UserMagicAttack = "CP_UserMagicAttack";
    public const string UserHit = "CP_UserHit";
    public const string DropPickUpRequest = "CP_DropPickUpRequest";
    public const string UserDropMoneyRequest = "CP_UserDropMoneyRequest";
    public const string UserStatChangeItemUseRequest = "CP_UserStatChangeItemUseRequest";
    public const string UserChangeSlotPositionRequest = "CP_UserChangeSlotPositionRequest";
    public const string UserGivePopularityRequest = "CP_UserGivePopularityRequest";
    public const string UserCharacterInfoRequest = "CP_UserCharacterInfoRequest";
    public const string UserSkillUpRequest = "CP_UserSkillUpRequest";
    public const string UserAbilityUpRequest = "CP_UserAbilityUpRequest";
    public const string UserAbilityMassUpRequest = "CP_UserAbilityMassUpRequest";
    public const string MobMove = "CP_MobMove";
    public const string UserTransferFieldRequest = "CP_UserTransferFieldRequest";
    public const string UserSelectNpc = "CP_UserSelectNpc";
    public const string UserShopRequest = "CP_UserShopRequest";
    public const string UserPortalScriptRequest = "CP_UserPortalScriptRequest";
    public const string UserScriptMessageAnswer = "CP_UserScriptMessageAnswer";
    public const string Whisper = "CP_Whisper";
    public const string Messenger = "CP_Messenger";
    public const string PartyRequest = "CP_PartyRequest";
}

/// <summary>
/// Well-known outbound (server → client) opcode <b>names</b>. Resolve to numeric values via
/// <see cref="OpcodeTable"/>. Names mirror upstream <c>ServerPacketHeader</c>.
/// </summary>
public static class ServerOpcode
{
    public const string CheckPasswordResult = "LP_CheckPasswordResult";
    public const string WorldInformation = "LP_WorldInformation";
    public const string SelectWorldResult = "LP_SelectWorldResult";
    public const string CheckDuplicatedIdResult = "LP_CheckDuplicatedIDResult";
    public const string CreateNewCharacterResult = "LP_CreateNewCharacterResult";
    public const string DeleteCharacterResult = "LP_DeleteCharacterResult";
    public const string ViewAllCharResult = "LP_ViewAllCharResult";
    public const string SelectCharacterResult = "LP_SelectCharacterResult";
    public const string RecommendWorldMessage = "LP_RecommendWorldMessage";
    public const string LatestConnectedWorld = "LP_LatestConnectedWorld";
    public const string MigrateCommand = "LP_MigrateCommand";
    public const string AliveReq = "LP_AliveReq";
    public const string JmsSetMapLogin = "LP_JMS_SetMapLogin";
    public const string SetField = "LP_SetField";
    public const string UserEnterField = "LP_UserEnterField";
    public const string UserLeaveField = "LP_UserLeaveField";
    public const string UserAvatarModified = "LP_UserAvatarModified";
    public const string TransferFieldReqIgnored = "LP_TransferFieldReqIgnored";
    public const string UserChat = "LP_UserChat";
    public const string UserEmotion = "LP_UserEmotion";
    public const string UserSitResult = "LP_UserSitResult";
    public const string Whisper = "LP_Whisper";
    public const string UserEffectLocal = "LP_UserEffectLocal";
    public const string UserEffectRemote = "LP_UserEffectRemote";
    public const string Messenger = "LP_Messenger";
    public const string PartyResult = "LP_PartyResult";
    public const string UserHP = "LP_UserHP";
    public const string GivePopularityResult = "LP_GivePopularityResult";
    public const string FieldEffect = "LP_FieldEffect";
    public const string Message = "LP_Message";
    public const string CharacterInfo = "LP_CharacterInfo";
    public const string UserMove = "LP_UserMove";
    public const string UserMeleeAttack = "LP_UserMeleeAttack";
    public const string UserShootAttack = "LP_UserShootAttack";
    public const string UserMagicAttack = "LP_UserMagicAttack";
    public const string ScriptMessage = "LP_ScriptMessage";
    public const string StatChanged = "LP_StatChanged";
    public const string InventoryOperation = "LP_InventoryOperation";
    public const string OpenShopDlg = "LP_OpenShopDlg";
    public const string ShopResult = "LP_ShopResult";
    public const string ForcedStatReset = "LP_ForcedStatReset";
    public const string FuncKeyMappedInit = "LP_FuncKeyMappedInit";
    public const string MacroSysDataInit = "LP_MacroSysDataInit";
    public const string PetConsumeItemInit = "LP_PetConsumeItemInit";
    public const string PetConsumeMpItemInit = "LP_PetConsumeMPItemInit";
    public const string PetConsumeCureItemInit = "LP_JMS_PetConsumeCureItemInit";
    public const string FriendResult = "LP_FriendResult";
    public const string FamilyInfoResult = "LP_FamilyInfoResult";
    public const string ChangeSkillRecordResult = "LP_ChangeSkillRecordResult";
    public const string BroadcastMsg = "LP_BroadcastMsg";
    public const string NpcEnterField = "LP_NpcEnterField";
    public const string MobEnterField = "LP_MobEnterField";
    public const string MobLeaveField = "LP_MobLeaveField";
    public const string MobChangeController = "LP_MobChangeController";
    public const string MobMove = "LP_MobMove";
    public const string MobCtrlAck = "LP_MobCtrlAck";
    public const string DropEnterField = "LP_DropEnterField";
    public const string DropLeaveField = "LP_DropLeaveField";
}
