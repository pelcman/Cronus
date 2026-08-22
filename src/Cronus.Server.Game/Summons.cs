namespace Cronus.Server.Game;

/// <summary>
/// The pre-BB summon skill tables (ports <c>MapleStatEffect.getSummonMovementType</c> /
/// <c>MapleSummon.isPuppet/getSummonType</c>). A skill listed here spawns a field summon when
/// cast; anything else is a plain buff/attack skill.
/// </summary>
public static class SummonSkills
{
    // m_nMoveAbility values (OpsMoveAbility).
    public const byte MoveStop = 0;
    public const byte MoveWalk = 1;
    public const byte MoveFly = 4;
    public const byte MoveFlyRandom = 5;

    // m_nAssistType values (OpsAssist).
    public const byte AssistNone = 0;
    public const byte AssistAttack = 1;
    public const byte AssistHeal = 2;

    public const int Beholder = 1321007;
    public const int Gaviota = 5211002;

    /// <summary>The summon's movement ability, or null when the skill isn't a summon.</summary>
    public static byte? MoveAbilityOf(int skillId) => skillId switch
    {
        3111002 or 3211002 or 13111004 => MoveStop,          // puppets
        5211001 or 5220002 => MoveStop,                      // octopus
        3111005 or 3211005 => MoveFly,                       // silver hawk / golden eagle
        2311006 or 3221005 or 3121006 => MoveFly,            // summon dragon / frostprey / phoenix
        Gaviota => MoveFlyRandom,                            // gaviota (departs after attacking)
        Beholder => MoveWalk,                                // beholder
        2121005 or 2221005 or 2321003 => MoveWalk,           // elquines / ifrit / bahamut
        12111004 or 11001004 or 12001004 or 13001004 or 14001005 or 15001004 => MoveWalk, // KoC
        _ => null,
    };

    public static bool IsSummon(int skillId) => MoveAbilityOf(skillId) is not null;

    public static bool IsPuppet(int skillId) => skillId is 3111002 or 3211002 or 13111004;

    public static byte AssistTypeOf(int skillId) => skillId switch
    {
        _ when IsPuppet(skillId) => AssistNone,
        Beholder => AssistHeal,
        _ => AssistAttack,
    };
}

/// <summary>A summon standing in a field (ports <c>MapleSummon</c>).</summary>
public sealed class FieldSummon
{
    public required int ObjectId { get; init; }

    public required int OwnerId { get; init; }

    public required int SkillId { get; init; }

    public required int SkillLevel { get; init; }

    /// <summary>The owner's level at cast time (the enter packet carries it).</summary>
    public required int OwnerLevel { get; init; }

    public short X { get; set; }

    public short Y { get; set; }

    public short Foothold { get; set; }

    /// <summary>Remaining HP; only puppets take hits.</summary>
    public int Hp { get; set; }

    public DateTime ExpiresAt { get; init; }

    public byte MoveAbility => SummonSkills.MoveAbilityOf(SkillId) ?? SummonSkills.MoveStop;

    public byte AssistType => SummonSkills.AssistTypeOf(SkillId);

    public bool IsPuppet => SummonSkills.IsPuppet(SkillId);
}
