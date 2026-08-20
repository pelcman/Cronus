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
    public const string ViewAllCharResult = "LP_ViewAllCharResult";
    public const string SelectCharacterResult = "LP_SelectCharacterResult";
    public const string RecommendWorldMessage = "LP_RecommendWorldMessage";
    public const string LatestConnectedWorld = "LP_LatestConnectedWorld";
    public const string MigrateCommand = "LP_MigrateCommand";
    public const string AliveReq = "LP_AliveReq";
    public const string SetField = "LP_SetField";
}
