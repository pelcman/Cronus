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

    /// <summary>Cash-shop currency (Nexon Points).</summary>
    public int NexonPoint { get; set; }

    /// <summary>Cash-shop secondary currency (Maple Points).</summary>
    public int MaplePoint { get; set; }

    /// <summary>The cash-shop locker: bought items awaiting a move to a character's inventory
    /// (per account; persisted as a JSON column).</summary>
    public Dictionary<long, CashLockerItem> CashLocker { get; set; } = new();
}

/// <summary>One item sitting in the account's cash-shop locker.</summary>
public sealed class CashLockerItem
{
    /// <summary>The cash unique id (the wire's 8-byte item SN).</summary>
    public long CashId { get; set; }

    public int ItemId { get; set; }

    public short Quantity { get; set; } = 1;

    /// <summary>The commodity serial it was bought as (Etc.wz Commodity SN).</summary>
    public int CommoditySn { get; set; }
}
