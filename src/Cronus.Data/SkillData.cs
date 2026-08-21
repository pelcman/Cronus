using System.Collections.Concurrent;

namespace Cronus.Data;

/// <summary>Provides a skill's maximum level (from Skill wz data).</summary>
public interface ISkillProvider
{
    /// <summary>Max level for <paramref name="skillId"/>, or 0 when unknown (no data / caller decides).</summary>
    int GetMaxLevel(int skillId);
}

/// <summary>
/// Loads skill max-levels from a wz_xml tree: <c>Skill/{skillId/10000:000}.img.xml</c> →
/// <c>skill/{skillId}/level</c> (max level = number of level entries). Results are cached per
/// skill id; unknown/missing skills return 0.
/// </summary>
public sealed class WzSkillProvider : ISkillProvider
{
    private readonly string _wzRoot;
    private readonly ConcurrentDictionary<int, int> _cache = new();

    public WzSkillProvider(string wzRoot) => _wzRoot = wzRoot;

    public int GetMaxLevel(int skillId) => _cache.GetOrAdd(skillId, Load);

    private int Load(int skillId)
    {
        string path = SkillImagePath(_wzRoot, skillId);
        return File.Exists(path) ? MaxLevelFromWz(WzData.ParseFile(path), skillId) : 0;
    }

    /// <summary>
    /// Reads a skill's max level from a parsed Skill <c>.img</c> (<c>skill/{id}/level</c> child
    /// count). Skill node names carry the id (sometimes zero-padded), so match by numeric value.
    /// Returns 0 when the skill isn't in this document.
    /// </summary>
    public static int MaxLevelFromWz(WzData skillImg, int skillId)
    {
        WzData? skillDir = skillImg.Child("skill");
        if (skillDir is null)
        {
            return 0;
        }

        foreach (WzData skill in skillDir.Children.Values)
        {
            if (int.TryParse(skill.Name, out int id) && id == skillId)
            {
                return skill.Child("level")?.Children.Count ?? 0;
            }
        }

        return 0;
    }

    /// <summary>The Skill <c>.img.xml</c> file for a skill: named by the job (skillId / 10000).</summary>
    public static string SkillImagePath(string wzRoot, int skillId)
        => Path.Combine(wzRoot, "Skill", $"{skillId / 10000:000}.img.xml");
}

/// <summary>An <see cref="ISkillProvider"/> with no data — every skill is "unknown" (max 0).</summary>
public sealed class NullSkillProvider : ISkillProvider
{
    public static readonly NullSkillProvider Instance = new();

    public int GetMaxLevel(int skillId) => 0;
}
