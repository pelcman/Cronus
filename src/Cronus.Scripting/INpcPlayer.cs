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

    /// <summary>Sets HP (clamped to 1..MaxHp) and tells the client — Roger's tutorial drops HP to show potions.</summary>
    void setHp(int hp);

    /// <summary>Remembers the current map so a later script can send the player back.</summary>
    void rememberMap();

    /// <summary>Warps to the remembered map (or the fallback when none) and forgets it.</summary>
    void warpToRememberedMap(int fallbackMapId);

    /// <summary>
    /// Warps the player to <paramref name="mapId"/>, spawn portal <paramref name="portal"/> (0 = default).
    /// One method, not two overloads: Jint mis-resolves an overloaded method after a blocking dialog
    /// call and reports it "not a function", so warp must stay a single signature.
    /// </summary>
    void warp(int mapId, int portal = 0);

    /// <summary>Warps to the portal named <paramref name="portalName"/> (the wz <c>pn</c>), or portal 0 when the map has no such portal.</summary>
    void warpPortal(int mapId, string portalName);

    /// <summary>Airship stations: true while boarding is open for the next departure.</summary>
    bool airshipBoarding();

    /// <summary>Airship stations: minutes until the next departure.</summary>
    int airshipMinutes();

    /// <summary>Opens the parcel (宅配) send window.</summary>
    void openParcel();

    /// <summary>Parcels waiting for this character.</summary>
    int parcelCount();

    /// <summary>Hands over the waiting parcels; returns how many were delivered.</summary>
    int receiveParcels();

    /// <summary>Adds (or removes) ability points, floored at zero, and notifies the client.</summary>
    void gainAp(int amount);

    /// <summary>Adds (or removes) skill points, floored at zero, and notifies the client.</summary>
    void gainSp(int amount);

    /// <summary>Adds (or removes) fame, clamped to ±30000, and notifies the client.</summary>
    void gainFame(int amount);

    /// <summary>Sets the player's job (e.g. a job-advancement NPC) and notifies the client.</summary>
    void setJob(int job);

    /// <summary>
    /// Job advancement (ports the oracle's MapleCharacter.changeJob): the job, its advancement SP
    /// (1; +2 for a fourth-tier job; a late first job also gets the 3 per level past 10 — 8 for a
    /// magician), the job's max HP/MP bonus with a refill, and the client / field notifications.
    /// The stat distribution is kept — see <see cref="resetStatsForJob"/>.
    /// </summary>
    void changeJob(int job);

    /// <summary>The Cygnus / Aran first-job stat reset: the job's base stats with every other point
    /// returned to AP (the oracle's resetStatsByJob; Cosmic's resetStats). No-op outside a first job.</summary>
    void resetStatsForJob();

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

    /// <summary>True when the player leads their party, or has none.</summary>
    bool isPartyLeader();

    /// <summary>Kerning Subway massacre: puts the party into a free stage-1 instance. False when all five are busy.</summary>
    bool startSubwayMassacre();

    /// <summary>Kerning Subway bonus (the 999 carriage) for this player alone. False when all are busy.</summary>
    bool bonusSubwayMassacre();

    /// <summary>A quest record's custom data (the oracle's <c>getQuestNAdd(id).getCustomData()</c>), or null.</summary>
    string? getQuestData(int questId);

    void setQuestData(int questId, string data);

    // 武陵道場 (Mu Lung Dojo). Points persist in a quest record; the entrance NPC and the
    // dojang_* portals drive the rest. Party play and the vanquisher medal are not wired yet.
    int dojoPoints();
    void setDojoPoints(int points);
    bool dojoEnter(bool party, int fromStage);
    void dojoNextStage();
    bool dojoTeleportUp();
    void dojoExit();
    bool dojoTutorialExit();

    /// <summary>Opens another NPC's dialog from a script (the job halls' tutorial portal opens the
    /// instructor); the same path as the /talk command. Ends any conversation already open.</summary>
    void openNpc(int npcId);

    /// <summary>The server's local hour (0–23) — some quests only work at certain times of day.</summary>
    int hourOfDay();

    /// <summary>Plays a screen effect from the client's Map.wz/Effect (the oracle's FieldEffect_Screen),
    /// e.g. "temaD/enter/mushCatle" — the themed-dungeon title shown on entering キノコ城.</summary>
    void showScreenEffect(string path);

    /// <summary>Opens the NPC shop with the given shop id (no-op when unknown).</summary>
    void openShop(int shopId);

    /// <summary>Opens the player's account storage (the trunk).</summary>
    void openStorage();

    /// <summary>Spawns a mob at the player's position (boss altars, event NPCs).</summary>
    void spawnMob(int mobId, int count);

    /// <summary>How many mobs are alive in the player's current map.</summary>
    int mobCount();

    /// <summary>True if the player's hired merchant is standing somewhere.</summary>
    bool hasMerchant();

    /// <summary>
    /// Packs up the player's hired merchant from afar: stock and banked meso return to them.
    /// Returns false when they have none.
    /// </summary>
    bool retrieveMerchant();

    /// <summary>The buddy list's current maximum size.</summary>
    int getBuddyCapacity();

    /// <summary>Grows the buddy list by <paramref name="amount"/> slots (capped at 100).</summary>
    void gainBuddyCapacity(int amount);
}
