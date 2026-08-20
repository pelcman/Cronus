namespace Cronus.Server.Login;

/// <summary>
/// Authenticates a MapleID / password against the account store (ports the essence of
/// <c>ReqCLogin.checkLogin</c>): auto-register unknown accounts, a trailing <c>_</c> on a
/// long-enough id selects female gender, and (optionally) a <c>GM</c> prefix flags a GM.
/// </summary>
public sealed class LoginService
{
    private readonly IAccountRepository _accounts;
    private readonly bool _autoRegister;
    private readonly bool _gmPrefix;

    public LoginService(IAccountRepository accounts, bool autoRegister = true, bool gmPrefix = false)
    {
        _accounts = accounts;
        _autoRegister = autoRegister;
        _gmPrefix = gmPrefix;
    }

    public readonly record struct Outcome(LoginResult Result, Account? Account);

    public Outcome Authenticate(string mapleId, string password)
    {
        if (string.IsNullOrEmpty(mapleId))
        {
            return new Outcome(LoginResult.NotRegistered, null);
        }

        // Trailing underscore on a >=5-char id means "female mode" (upstream endwith_).
        bool female = false;
        if (mapleId.Length >= 5 && mapleId.EndsWith('_'))
        {
            mapleId = mapleId[..^1];
            female = true;
        }

        bool gm = _gmPrefix && mapleId.StartsWith("GM", StringComparison.Ordinal);

        Account? account = _accounts.Find(mapleId);
        if (account is null)
        {
            if (!_autoRegister)
            {
                return new Outcome(LoginResult.NotRegistered, null);
            }

            account = _accounts.Create(mapleId, password, gender: (byte)(female ? 1 : 0));
        }
        else if (!string.Equals(account.Password, password, StringComparison.Ordinal))
        {
            return new Outcome(LoginResult.IncorrectPassword, null);
        }

        if (female)
        {
            account.Gender = 1;
        }

        if (gm)
        {
            account.IsGameMaster = true;
        }

        return new Outcome(LoginResult.Success, account);
    }
}
