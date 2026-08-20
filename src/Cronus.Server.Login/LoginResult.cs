namespace Cronus.Server.Login;

/// <summary>
/// Login result codes sent as the first byte of <c>LP_CheckPasswordResult</c>. Values match
/// upstream <c>OpsLogin</c> (the subset the login flow uses).
/// </summary>
public enum LoginResult
{
    ProcFail = -1,
    Success = 0,
    TempBlocked = 1,
    Blocked = 2,
    Abandoned = 3,
    IncorrectPassword = 4,
    NotRegistered = 5,
    DbFail = 6,
    AlreadyConnected = 7,
}
