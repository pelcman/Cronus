using System.Text;
using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

/// <summary>
/// The server-relevant part of a mob's attacks (ports MobWz.getMobAttackInfo): attack{n}/info
/// flags, 0-based attack index, and the info/link hop to a borrowed attack table.
/// </summary>
public class MobAttackInfoTests
{
    // Real shapes from gamedata: 8180000 (deadly attack1), 8110300 (mpBurn attack1),
    // 5130107 (poison attack2). The nested range/hit dirs come BEFORE the flat ints, as in the wz.
    private const string DeadlyXml = """
        <imgdir name="8180000.img">
          <imgdir name="info"><int name="maxHP" value="1000"/><int name="level" value="90"/></imgdir>
          <imgdir name="attack1">
            <imgdir name="info">
              <imgdir name="range"><vector name="lt" x="-300" y="-200"/><vector name="rb" x="30" y="10"/></imgdir>
              <imgdir name="hit"><canvas name="0" width="61" height="48"><int name="delay" value="130"/></canvas></imgdir>
              <int name="type" value="0"/>
              <int name="conMP" value="1"/>
              <int name="attackAfter" value="1750"/>
              <int name="deadlyAttack" value="1"/>
              <int name="magic" value="1"/>
            </imgdir>
          </imgdir>
        </imgdir>
        """;

    private const string PoisonXml = """
        <imgdir name="5130107.img">
          <imgdir name="info"><int name="maxHP" value="500"/></imgdir>
          <imgdir name="attack1"><imgdir name="info"><int name="type" value="0"/><int name="attackAfter" value="500"/></imgdir></imgdir>
          <imgdir name="attack2">
            <imgdir name="info">
              <int name="type" value="0"/>
              <int name="conMP" value="5"/>
              <int name="magic" value="1"/>
              <int name="disease" value="125"/>
              <int name="level" value="1"/>
              <int name="mpBurn" value="400"/>
            </imgdir>
          </imgdir>
        </imgdir>
        """;

    private const string LinkedXml = """
        <imgdir name="8180001.img">
          <imgdir name="info"><int name="maxHP" value="20"/><int name="link" value="8180000"/></imgdir>
        </imgdir>
        """;

    [Fact]
    public void ParsesDeadlyAttack_ByKeyPresence()
    {
        MobData mob = MobData.FromWz(8180000, WzData.ParseText(DeadlyXml));

        MobAttackInfo a = mob.AttackAt(0);              // attack1 is index 0 (CP_UserHit's nAttackIdx)
        Assert.True(a.DeadlyAttack);
        Assert.Equal(1, a.MpCon);
        Assert.Equal(0, a.MpBurn);
        Assert.Equal(0, a.DiseaseSkill);
        Assert.Same(MobAttackInfo.None, mob.AttackAt(1)); // no attack2
    }

    [Fact]
    public void ParsesMpBurnAndDisease_AndSkipsPlainAttacks()
    {
        MobData mob = MobData.FromWz(5130107, WzData.ParseText(PoisonXml));

        Assert.Same(MobAttackInfo.None, mob.AttackAt(0)); // attack1 carries nothing server-side
        MobAttackInfo a = mob.AttackAt(1);
        Assert.False(a.DeadlyAttack);
        Assert.Equal(400, a.MpBurn);
        Assert.Equal(125, a.DiseaseSkill);
        Assert.Equal(1, a.DiseaseLevel);
        Assert.Equal(5, a.MpCon);
    }

    private sealed class MemoryStore : IWzStore
    {
        private readonly Dictionary<string, string> _docs;

        public MemoryStore(Dictionary<string, string> docs) => _docs = docs;

        public string? ReadText(string relativePath) => _docs.TryGetValue(relativePath, out string? s) ? s : null;

        public bool Exists(string relativePath) => _docs.ContainsKey(relativePath);

        public IEnumerable<string> EnumeratePaths(string prefix) => _docs.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal));
    }

    [Fact]
    public void Provider_FollowsInfoLink_ForTheAttackTable()
    {
        var provider = new WzMobProvider(new MemoryStore(new Dictionary<string, string>
        {
            [WzMobProvider.MobImageRel(8180000)] = DeadlyXml,
            [WzMobProvider.MobImageRel(8180001)] = LinkedXml,
        }));

        MobData linked = provider.GetMob(8180001)!;

        Assert.Equal(20, linked.MaxHp);                  // its own stats
        Assert.Equal(8180000, linked.Link);
        Assert.True(linked.AttackAt(0).DeadlyAttack);    // the link target's attacks
    }
}
