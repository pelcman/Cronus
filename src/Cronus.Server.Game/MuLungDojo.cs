namespace Cronus.Server.Game;

/// <summary>
/// The 武陵道場 (Mu Lung Dojo) tables and map arithmetic, ported from the oracle's
/// <c>odin.server.maps.Event_DojoAgent</c> (stage → mob, points, clock) and cross-checked against
/// the JMS v186 client's map data (925020000 solo / 925030000 party, 38 stages × up to 15 copies,
/// fighting maps have fieldType 14). Pure and side-effect-free so it can be unit-tested; the live
/// wiring (finding a free instance, spawning, the clock and the screen effects) is in
/// <c>ChannelHandler.Dojo</c>. Party play, the vanquisher medal and the special back-street floor
/// are not modelled here yet — those paths are marked <c>[DEV]</c> where the player would meet them.
/// </summary>
public static class MuLungDojo
{
    /// <summary>Solo instance base: 925020000 + stage×100 + copy.</summary>
    public const int SoloBase = 925020000;

    /// <summary>Party instance base (not entered yet — [DEV]).</summary>
    public const int PartyBase = 925030000;

    /// <summary>The dojo hall (So Gong's lobby, 925020000).</summary>
    public const int Hall = 925020000;

    /// <summary>So Gong's challenge room (925020001), where the alone/party menu opens.</summary>
    public const int Entrance = 925020001;

    /// <summary>The exit hall (925020002): where a cleared/failed run lands.</summary>
    public const int Exit = 925020002;

    /// <summary>The rooftop (925020003): the reward map after clearing stage 38.</summary>
    public const int Roof = 925020003;

    /// <summary>The tutorial fighting map base (925020010..): first-timers fight So Gong's agent here.</summary>
    public const int TutorialBase = 925020010;

    /// <summary>Highest stage.</summary>
    public const int MaxStage = 38;

    /// <summary>How many copies of each stage the client ships (indexed 0..14).</summary>
    public const int Copies = 15;

    /// <summary>The invincible "cleared" checker mob (spawned by every stage boss's <c>revive</c>).</summary>
    public const int ClearChecker = 9300216;

    /// <summary>The tutorial agent mob (925020010), and its checker is the same 9300216.</summary>
    public const int TutorialMob = 9300269;

    /// <summary>
    /// The dojo-points store lives in a quest record so it persists and rides the login blob like
    /// any quest (the massacre keeps its kill count the same way). 1207 is the id the JMS client
    /// reads for the on-screen "training points" info (<c>updateInfoQuest(1207, …)</c>).
    /// </summary>
    public const int PointsQuest = 1207;

    /// <summary>The five Mu Lung belts, ascending, and the level / points each needs (from the NPC).</summary>
    public static readonly int[] Belts = { 1132000, 1132001, 1132002, 1132003, 1132004 };
    public static readonly int[] BeltLevel = { 25, 35, 45, 60, 75 };
    public static readonly int[] BeltPoints = { 200, 1800, 4000, 9200, 17000 };

    /// <summary>True for a dojo map id (any instance, solo or party).</summary>
    public static bool IsDojo(int mapId) => mapId is >= 925020000 and <= 925033814;

    /// <summary>A fighting stage map (fieldType 14 in the wz): 92502SS C or 92503SS C, stage 1..38.</summary>
    public static bool IsStageMap(int mapId)
    {
        if (mapId is >= SoloBase + 100 and <= SoloBase + (MaxStage * 100) + (Copies - 1))
        {
            return true;
        }

        return mapId is >= PartyBase + 100 and <= PartyBase + (MaxStage * 100) + (Copies - 1);
    }

    /// <summary>The tutorial fighting map (925020010..925020015).</summary>
    public static bool IsTutorialMap(int mapId) => mapId is >= TutorialBase and <= TutorialBase + 5;

    /// <summary>The stage number a fighting map is (1..38), or 0 when it is not a stage map.</summary>
    public static int StageOf(int mapId) => IsStageMap(mapId) ? (mapId / 100) % 100 : 0;

    /// <summary>The copy index (0..14) of a fighting map.</summary>
    public static int CopyOf(int mapId) => mapId % 100;

    /// <summary>The base (solo/party) a fighting map belongs to.</summary>
    public static int BaseOf(int mapId) => mapId >= PartyBase ? PartyBase : SoloBase;

    /// <summary>The map id of <paramref name="stage"/>, same base and copy as <paramref name="fromMap"/>.</summary>
    public static int StageMap(int fromMap, int stage) => BaseOf(fromMap) + (stage * 100) + CopyOf(fromMap);

    /// <summary>A resting spot: every sixth stage (6, 12, 18, 24, 30, 36) has no boss, So Gong waits there.</summary>
    public static bool IsRestingStage(int stage) => stage > 0 && stage % 6 == 0;

    /// <summary>The boss for a stage (Event_DojoAgent.spawnMonster). 0 for the resting stages (6,12,…) and out of range.</summary>
    public static int MobForStage(int stage) => stage switch
    {
        1 => 9300184, 2 => 9300185, 3 => 9300186, 4 => 9300187, 5 => 9300188,
        7 => 9300189, 8 => 9300190, 9 => 9300191, 10 => 9300192, 11 => 9300193,
        13 => 9300194, 14 => 9300195, 15 => 9300196, 16 => 9300197, 17 => 9300198,
        19 => 9300199, 20 => 9300200, 21 => 9300201, 22 => 9300202, 23 => 9300203,
        25 => 9300204, 26 => 9300205, 27 => 9300206, 28 => 9300207, 29 => 9300208,
        31 => 9300209, 32 => 9300210, 33 => 9300211, 34 => 9300212, 35 => 9300213,
        37 => 9300214, 38 => 9300215,
        _ => 0,
    };

    /// <summary>Training points earned for clearing a stage (Event_DojoAgent.getDojoPoints, solo +1).</summary>
    public static int PointsForStage(int stage)
    {
        int band = stage switch
        {
            >= 1 and <= 5 => 1,
            >= 7 and <= 11 => 2,
            >= 13 and <= 17 => 3,
            >= 19 and <= 23 => 4,
            >= 25 and <= 29 => 5,
            >= 31 and <= 35 => 6,
            >= 37 and <= 38 => 7,
            _ => 0,
        };
        return band == 0 ? 0 : (band + 1) * 3; // the oracle's "(points+1)*3" solo award
    }

    /// <summary>Clock seconds for a stage (Event_DojoAgent.getTiming × 60): 5–15 minutes by band.</summary>
    public static int ClockSeconds(int stage)
    {
        int minutes = stage switch
        {
            >= 1 and <= 5 => 5,
            >= 7 and <= 11 => 6,
            >= 13 and <= 17 => 7,
            >= 19 and <= 23 => 8,
            >= 25 and <= 29 => 9,
            >= 31 and <= 35 => 10,
            >= 37 and <= 38 => 15,
            _ => 5,
        };
        return minutes * 60;
    }

    /// <summary>The stage number the client's start effect shows (band start, Event_DojoAgent.getDojoStageDec).</summary>
    public static int DisplayStage(int stage)
    {
        int dec = stage switch
        {
            >= 7 and <= 11 => 1,
            >= 13 and <= 17 => 2,
            >= 19 and <= 23 => 3,
            >= 25 and <= 29 => 4,
            >= 31 and <= 35 => 5,
            >= 37 and <= 38 => 6,
            _ => 0,
        };
        return stage - dec;
    }
}
