namespace Cronus.Server.Game;

/// <summary>
/// The classic Zakum fight gate: the body (three phases) cannot be damaged while any of the
/// eight arms still stands.
/// </summary>
public static class ZakumGate
{
    public const int SummonItem = 4001017; // 火の目 (Eye of Fire)

    public static bool IsBody(int templateId) => templateId is >= 8800000 and <= 8800002;

    public static bool IsArm(int templateId) => templateId is >= 8800003 and <= 8800010;

    /// <summary>True while a live arm protects the body from all damage.</summary>
    public static bool BodyProtected(IEnumerable<FieldMob> mobs)
        => mobs.Any(m => !m.IsDead && IsArm(m.TemplateId));
}
