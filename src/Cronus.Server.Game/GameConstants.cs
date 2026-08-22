namespace Cronus.Server.Game;

/// <summary>
/// Central gameplay tunables (the Maple2 <c>Constants.cs</c> pattern): every rule that an
/// operator might want to loosen or restore-to-authentic lives here with its reference value
/// noted, instead of being buried as a magic number in a handler. Change a value, rebuild, done.
/// </summary>
public static class GameConstants
{
    // ---- Monster Book -------------------------------------------------------------------

    /// <summary>Max registrations per card (reference: 5).</summary>
    public const int MonsterCardMaxCount = 5;

    /// <summary>
    /// A mob stops dropping its card once the killer has this many registered.
    /// This server's rule: 1 (one pickup ends the farm). Authentic reference behaviour: 5.
    /// </summary>
    public const int MonsterCardStopDropCount = 1;

    // Monster-Book packet switches (mutable: the /booktest command flips them live to bisect
    // a client crash — the LP_MonsterBookSetCard opcode is unverified against the real client).
    public static bool SendMonsterBookSetCard { get; set; } = true;
    public static bool SendMonsterBookCardEffect { get; set; } = true;
    public static bool SendMonsterBookCardMessage { get; set; } = true;

    /// <summary>
    /// The user-effect id for the card-get flash, tunable live via <c>/booktest effect &lt;n&gt;</c>.
    /// v186's table is neither the ≤147 pre-BB one (13 crashed) nor the GMS-v95 default (15
    /// crashed): with Aran in v186 the Resist entry shifts everything, giving the v302 layout
    /// minus the later JMS charm entry — CardGet 16 (and QuestComplete 12).
    /// </summary>
    public static byte MonsterBookCardEffectValue { get; set; } = 16;

    // ---- Social -------------------------------------------------------------------------

    /// <summary>Minimum level to give fame (reference: 15).</summary>
    public const int FameMinLevel = 15;

    /// <summary>Buddy-list starting capacity (reference: 20, expandable to 100).</summary>
    public const short BuddyDefaultCapacity = 20;

    // ---- Inventory ----------------------------------------------------------------------

    /// <summary>Slots per inventory tab (reference default: 24; the client renders this many).</summary>
    public const int InventorySlotsPerTab = 24;

    // ---- Economy ------------------------------------------------------------------------

    /// <summary>Smallest meso amount a player may throw on the ground (reference: 10).</summary>
    public const int MesoDropMin = 10;

    /// <summary>Largest meso amount a player may throw on the ground (reference: 50000).</summary>
    public const int MesoDropMax = 50_000;

    // ---- Restrictions -------------------------------------------------------------------

    /// <summary>
    /// When true, /gender also flips the ACCOUNT gender so the cash shop (which filters by the
    /// login-time account gender) sells the matching line after the next login. False keeps the
    /// account's original gender fixed, authentic-style.
    /// </summary>
    public const bool GenderCommandChangesAccount = true;
}
