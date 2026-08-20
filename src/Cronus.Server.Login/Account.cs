namespace Cronus.Server.Login;

/// <summary>A player account. Interim model backing the login flow before the DB layer lands.</summary>
public sealed class Account
{
    public required int Id { get; init; }

    /// <summary>The MapleID / login name (without any trailing gender-mode underscore).</summary>
    public required string LoginId { get; init; }

    /// <summary>Password verifier. Interim: stored as-is; TODO replace with a real hash.</summary>
    public required string Password { get; set; }

    /// <summary>0 = male, 1 = female.</summary>
    public byte Gender { get; set; }

    public bool IsGameMaster { get; set; }
}
