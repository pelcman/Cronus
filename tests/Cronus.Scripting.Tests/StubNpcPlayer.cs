// <auto> Reusable INpcPlayer stub: virtual no-op / neutral defaults so a test overrides only
// what it exercises. Generated from INpcPlayer; keep in sync if the interface grows.
namespace Cronus.Scripting.Tests;

public abstract class StubNpcPlayer : Cronus.Scripting.INpcPlayer
{
    public virtual string getName() => string.Empty;
    public virtual int getLevel() => 0;
    public virtual int getMapId() => 0;
    public virtual int getMeso() => 0;
    public virtual int getHp() => 0;
    public virtual int getMaxHp() => 0;
    public virtual int getExp() => 0;
    public virtual int getGender() => 0;
    public virtual int getJob() => 0;
    public virtual int getStr() => 0;
    public virtual int getDex() => 0;
    public virtual int getInt() => 0;
    public virtual int getLuk() => 0;
    public virtual int getFame() => 0;
    public virtual int getAp() => 0;
    public virtual int getSp() => 0;
    public virtual int getHair() => 0;
    public virtual int getFace() => 0;
    public virtual int getSkin() => 0;
    public virtual bool isValidStyle(int styleId) => false;
    public virtual void setHair(int hairId) { }
    public virtual void setFace(int faceId) { }
    public virtual void setSkin(int skinColor) { }
    public virtual void gainMeso(int amount) { }
    public virtual void gainExp(int amount) { }
    public virtual void heal() { }
    public virtual void rememberMap() { }
    public virtual void warpToRememberedMap(int fallbackMapId) { }
    public virtual void warp(int mapId) { }
    public virtual void warp(int mapId, int portal) { }
    public virtual void warpPortal(int mapId, string portalName) { }
    public virtual bool airshipBoarding() => false;
    public virtual int airshipMinutes() => 0;
    public virtual void openParcel() { }
    public virtual int parcelCount() => 0;
    public virtual int receiveParcels() => 0;
    public virtual void gainAp(int amount) { }
    public virtual void gainSp(int amount) { }
    public virtual void gainFame(int amount) { }
    public virtual void setJob(int job) { }
    public virtual void gainMaxHp(int amount) { }
    public virtual void gainMaxMp(int amount) { }
    public virtual bool hasQuest(int questId) => false;
    public virtual bool isQuestDone(int questId) => false;
    public virtual void startQuest(int questId) { }
    public virtual void completeQuest(int questId) { }
    public virtual void gainItem(int itemId, int quantity) { }
    public virtual bool haveItem(int itemId) => false;
    public virtual int itemQuantity(int itemId) => 0;
    public virtual bool isPartyLeader() => false;
    public virtual bool startSubwayMassacre() => false;
    public virtual bool bonusSubwayMassacre() => false;
    public virtual string? getQuestData(int questId) => null;
    public virtual void setQuestData(int questId, string data) { }
    public virtual int dojoPoints() => 0;
    public virtual void setDojoPoints(int points) { }
    public virtual bool dojoEnter(bool party, int fromStage) => false;
    public virtual void dojoNextStage() { }
    public virtual bool dojoTeleportUp() => false;
    public virtual void dojoExit() { }
    public virtual bool dojoTutorialExit() => false;
    public virtual void openShop(int shopId) { }
    public virtual void openStorage() { }
    public virtual void spawnMob(int mobId, int count) { }
    public virtual int mobCount() => 0;
    public virtual bool hasMerchant() => false;
    public virtual bool retrieveMerchant() => false;
    public virtual int getBuddyCapacity() => 0;
    public virtual void gainBuddyCapacity(int amount) { }
}
