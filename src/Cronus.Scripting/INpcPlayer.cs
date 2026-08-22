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

    int getHair();

    int getFace();

    int getSkin();

    /// <summary>
    /// True if the id names a hair (30000+), face (20000+), or skin color (&lt; 100) the client
    /// has data for — i.e. safe to pass to <c>setHair/setFace/setSkin</c> or an avatar picker.
    /// False for everything when the server runs without game data.
    /// </summary>
    bool isValidStyle(int styleId);

    /// <summary>Changes the hair (validated against game data) and shows it to the field.</summary>
    void setHair(int hairId);

    /// <summary>Changes the face (validated against game data) and shows it to the field.</summary>
    void setFace(int faceId);

    /// <summary>Changes the skin color (validated against game data) and shows it to the field.</summary>
    void setSkin(int skinColor);

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

    /// <summary>Raises max HP by <paramref name="amount"/> (clamped to 1..30000) and heals into it.</summary>
    void gainMaxHp(int amount);

    /// <summary>Raises max MP by <paramref name="amount"/> (clamped to 1..30000) and refills into it.</summary>
    void gainMaxMp(int amount);

    /// <summary>True if the quest is currently started (in progress).</summary>
    bool hasQuest(int questId);

    /// <summary>True if the quest has been completed.</summary>
    bool isQuestDone(int questId);

    /// <summary>Marks a quest as started.</summary>
    void startQuest(int questId);

    /// <summary>Marks a quest as completed (removing it from started).</summary>
    void completeQuest(int questId);

    /// <summary>Gives (or, if negative, takes) items, with a live inventory update.</summary>
    void gainItem(int itemId, int quantity);

    /// <summary>True if the player carries at least one of the item.</summary>
    bool haveItem(int itemId);

    /// <summary>How many of the item the player carries across all stacks.</summary>
    int itemQuantity(int itemId);

    /// <summary>Opens the NPC shop with the given shop id (no-op when unknown).</summary>
    void openShop(int shopId);

    /// <summary>Opens the player's account storage (the trunk).</summary>
    void openStorage();

    /// <summary>The buddy list's current maximum size.</summary>
    int getBuddyCapacity();

    /// <summary>Grows the buddy list by <paramref name="amount"/> slots (capped at 100).</summary>
    void gainBuddyCapacity(int amount);
}
