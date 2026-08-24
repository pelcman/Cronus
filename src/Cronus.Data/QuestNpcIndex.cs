using System.Text.RegularExpressions;

namespace Cronus.Data;

/// <summary>Answers "does any quest start or end at this NPC?".</summary>
public interface IQuestNpcIndex
{
    bool HasQuests(int npcId);
}

/// <summary>
/// One pass over <c>Quest/Check.img.xml</c> collecting every <c>npc</c> value (start and end
/// sides alike). The click handler uses this to tell quest NPCs apart from plain ones: a quest
/// NPC's click must stay unanswered so the client can run its own quest UI, while an NPC with
/// no script, no shop, and no quests gets a fallback dialog instead of dead silence.
/// </summary>
public sealed class WzQuestNpcIndex : IQuestNpcIndex
{
    private static readonly Regex NpcValue = new(
        "<int name=\"npc\" value=\"(\\d+)\"", RegexOptions.Compiled);

    private readonly Lazy<HashSet<int>> _npcIds;

    public WzQuestNpcIndex(IWzStore store) => _npcIds = new(() => Load(store));

    public bool HasQuests(int npcId) => _npcIds.Value.Contains(npcId);

    private static HashSet<int> Load(IWzStore store)
    {
        var ids = new HashSet<int>();
        if (store.ReadText("Quest/Check.img.xml") is { } xml)
        {
            foreach (Match m in NpcValue.Matches(xml))
            {
                if (int.TryParse(m.Groups[1].ValueSpan, out int id) && id > 0)
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }
}

/// <summary>An in-memory index for tests / seeded content.</summary>
public sealed class InMemoryQuestNpcIndex : IQuestNpcIndex
{
    private readonly HashSet<int> _ids;

    public InMemoryQuestNpcIndex(IEnumerable<int> npcIds) => _ids = npcIds.ToHashSet();

    public bool HasQuests(int npcId) => _ids.Contains(npcId);
}
