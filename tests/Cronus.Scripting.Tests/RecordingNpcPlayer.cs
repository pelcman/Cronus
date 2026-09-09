// Concrete INpcPlayer test player (implements the interface directly, like the production
// ChannelPlayer and the exerciser's ProbePlayer). A StubNpcPlayer (abstract base + virtual
// overrides) trips a Jint member-resolution quirk for `warp` after a blocking dialog call,
// so warp-driven tests use this concrete recorder instead.
namespace Cronus.Scripting.Tests;

public class RecordingNpcPlayer : Cronus.Scripting.INpcPlayer
{
    public readonly System.Collections.Generic.Dictionary<int,int> Inventory = new();
    public int Meso;
    public int Level = 70;
    public int MapId;
    public int Job;
    public string Name = "Tester";
    public (int Map, int Portal)? Warped;
    public (int Map, string Portal)? WarpedNamed;

    public string getName() => Name;
    public int getLevel() => Level;
    public int getMapId() => MapId;
    public int getMeso() => Meso;
    public int Hp = 100;
    public int getHp() => Hp;
    public void setHp(int hp) => Hp = hp;
    public int getMaxHp() => 0;
    public int getExp() => 0;
    public int getGender() => 0;
    public int getJob() => Job;
    public int getStr() => 0;
    public int getDex() => 0;
    public int getInt() => 0;
    public int getLuk() => 0;
    public int getFame() => 0;
    public int getAp() => 0;
    public int getSp() => 0;
    public int getHair() => 0;
    public int getFace() => 0;
    public int getSkin() => 0;
    public bool isValidStyle(int styleId) => false;
    public void setHair(int hairId) { }
    public void setFace(int faceId) { }
    public void setSkin(int skinColor) { }
    public void gainMeso(int amount) => Meso += amount;
    public int Exp;
    public void gainExp(int amount) => Exp += amount;
    public void heal() { }
    public void rememberMap() { }
    public void warpToRememberedMap(int fallbackMapId) { }
    public void warp(int mapId, int portal = 0) => Warped = (mapId, portal);
    public void warpPortal(int mapId, string portalName) => WarpedNamed = (mapId, portalName);
    public bool airshipBoarding() => false;
    public int airshipMinutes() => 0;
    public void openParcel() { }
    public int parcelCount() => 0;
    public int receiveParcels() => 0;
    public void gainAp(int amount) { }
    public void gainSp(int amount) { }
    public void gainFame(int amount) { }
    public void setJob(int job) { }
    public void gainMaxHp(int amount) { }
    public void gainMaxMp(int amount) { }
    public bool hasQuest(int questId) => StartedQuests.Contains(questId);
    public bool isQuestDone(int questId) => _done.Contains(questId);
    public readonly System.Collections.Generic.List<int> StartedQuests = new();
    public void startQuest(int questId) => StartedQuests.Add(questId);
    public readonly System.Collections.Generic.List<int> CompletedQuests = new();
    public void completeQuest(int questId) { CompletedQuests.Add(questId); _done.Add(questId); }
    public void gainItem(int itemId, int quantity) => Inventory[itemId] = Inventory.GetValueOrDefault(itemId) + quantity;
    public bool haveItem(int itemId) => Inventory.GetValueOrDefault(itemId) > 0;
    public int itemQuantity(int itemId) => Inventory.GetValueOrDefault(itemId);
    public bool isPartyLeader() => false;
    public bool startSubwayMassacre() => false;
    public bool bonusSubwayMassacre() => false;
    public readonly System.Collections.Generic.Dictionary<int,string> QuestData = new();
    public string? getQuestData(int questId) => QuestData.TryGetValue(questId, out string? d) ? d : null;
    public void setQuestData(int questId, string data) => QuestData[questId] = data;
    public int dojoPoints() => 0;
    public void setDojoPoints(int points) { }
    public bool dojoEnter(bool party, int fromStage) => false;
    public void dojoNextStage() { }
    public bool dojoTeleportUp() => false;
    public void dojoExit() { }
    public bool dojoTutorialExit() => false;
    public void openShop(int shopId) { }
    public void openStorage() { }
    public void spawnMob(int mobId, int count) { }
    public int mobCount() => 0;
    public bool hasMerchant() => false;
    public bool retrieveMerchant() => false;
    public int getBuddyCapacity() => 0;
    public void gainBuddyCapacity(int amount) { }
    public int Hour = 12;
    public int hourOfDay() => Hour;
    public readonly System.Collections.Generic.List<string> ScreenEffects = new();
    public void showScreenEffect(string path) => ScreenEffects.Add(path);
    public int? OpenedNpc;
    public void openNpc(int npcId) => OpenedNpc = npcId;
    private readonly System.Collections.Generic.HashSet<int> _done = new();
    public void SetQuestDone(params int[] ids) { foreach (var i in ids) _done.Add(i); }
}
