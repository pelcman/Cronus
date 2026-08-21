namespace Cronus.Scripting;

/// <summary>
/// The player surface exposed to NPC scripts as the global <c>player</c>. Methods are named in
/// the OdinMS style (getX / gainX) so existing scripts read naturally. Kept intentionally small
/// and read-mostly; field-mutating actions (warp, spawn) are added as the channel gains safe
/// hooks for them.
/// </summary>
public interface INpcPlayer
{
    string getName();

    int getLevel();

    int getMapId();

    int getMeso();

    int getHp();

    int getMaxHp();

    int getExp();

    /// <summary>Adds (or, if negative, removes) mesos, clamped at zero, and persists.</summary>
    void gainMeso(int amount);

    /// <summary>Adds experience (no auto-level yet) and notifies the client.</summary>
    void gainExp(int amount);

    /// <summary>Restores HP and MP to full and notifies the client.</summary>
    void heal();

    /// <summary>Warps the player to another map's default spawn portal.</summary>
    void warp(int mapId);

    /// <summary>Warps the player to a specific spawn portal of another map.</summary>
    void warp(int mapId, int portal);

    /// <summary>True if the quest is currently started (in progress).</summary>
    bool hasQuest(int questId);

    /// <summary>True if the quest has been completed.</summary>
    bool isQuestDone(int questId);

    /// <summary>Marks a quest as started.</summary>
    void startQuest(int questId);

    /// <summary>Marks a quest as completed (removing it from started).</summary>
    void completeQuest(int questId);
}
