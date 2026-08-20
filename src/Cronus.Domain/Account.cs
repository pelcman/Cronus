namespace Cronus.Domain;

/// <summary>A player account. Shared domain entity persisted by the database layer.</summary>
public sealed class Account
{
    public int Id { get; set; }

    /// <summary>The MapleID / login name (without any trailing gender-mode underscore).</summary>
    public required string LoginId { get; set; }

    /// <summary>Password verifier. Interim: stored as-is; TODO replace with a real hash.</summary>
    public required string Password { get; set; }

    /// <summary>0 = male, 1 = female.</summary>
    public byte Gender { get; set; }

    public bool IsGameMaster { get; set; }
}
