namespace Cronus.Server.Login;

/// <summary>
/// Password hashing for account credentials (BCrypt). Stored values are self-describing
/// (<c>$2…</c>), so legacy plaintext rows are recognizable and upgraded in place on their next
/// successful login — no migration step needed.
/// </summary>
public static class PasswordHasher
{
    /// <summary>Work factor 10 ≈ tens of ms per attempt — enough to blunt online guessing
    /// without making the login screen feel slow.</summary>
    private const int WorkFactor = 10;

    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    /// <summary>True when <paramref name="stored"/> is a BCrypt hash rather than legacy plaintext.</summary>
    public static bool IsHashed(string stored) => stored.StartsWith("$2", StringComparison.Ordinal);

    /// <summary>Constant-time verify of <paramref name="password"/> against a BCrypt hash.</summary>
    public static bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception)
        {
            return false; // malformed hash: fail closed
        }
    }
}
