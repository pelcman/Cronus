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

    int getGender();

    int getJob();

    int getStr();

    int getDex();

    int getInt();

    int getLuk();

    int getFame();

    int getAp();

    int getSp();

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

    /// <summary>Adds (or removes) ability points, floored at zero, and notifies the client.</summary>
    void gainAp(int amount);

    /// <summary>Adds (or removes) skill points, floored at zero, and notifies the client.</summary>
    void gainSp(int amount);

    /// <summary>Adds (or removes) fame, clamped to ±30000, and notifies the client.</summary>
    void gainFame(int amount);

    /// <summary>Sets the player's job (e.g. a job-advancement NPC) and notifies the client.</summary>
    void setJob(int job);

    /// <summary>True if the quest is currently started (in progress).</summary>
    bool hasQuest(int questId);

    /// <summary>True if the quest has been completed.</summary>
    bool isQuestDone(int questId);

    /// <summary>Marks a quest as started.</summary>
    void startQuest(int questId);

    /// <summary>Marks a quest as completed (removing it from started).</summary>
    void completeQuest(int questId);

    /// <summary>Opens the NPC shop with the given shop id (no-op when unknown).</summary>
    void openShop(int shopId);

    /// <summary>Opens the player's account storage (the trunk).</summary>
    void openStorage();
}
