using System.Text;
using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

public class MobDataTests
{
    private const string MobXml = """
        <imgdir name="0100100.img">
          <imgdir name="info">
            <int name="maxHP" value="50"/>
            <int name="maxMP" value="0"/>
            <int name="exp" value="25"/>
            <int name="level" value="3"/>
          </imgdir>
        </imgdir>
        """;

    private const string BossXml = """
        <imgdir name="8800000.img">
          <imgdir name="info">
            <int name="maxHP" value="30000"/>
            <int name="exp" value="5000"/>
            <int name="level" value="50"/>
            <int name="hpTagColor" value="4"/>
            <int name="hpTagBgcolor" value="6"/>
          </imgdir>
        </imgdir>
        """;

    [Fact]
    public void ParsesMobStats()
    {
        WzData wz = WzData.Parse(new MemoryStream(Encoding.UTF8.GetBytes(MobXml)));
        MobData mob = MobData.FromWz(100100, wz);

        Assert.Equal(100100, mob.TemplateId);
        Assert.Equal(50, mob.MaxHp);
        Assert.Equal(25, mob.Exp);
        Assert.Equal(3, mob.Level);
        Assert.Equal(0, mob.TagColor);   // ordinary mob: no boss HP gauge
        Assert.Equal(0, mob.TagBgColor);
    }

    [Fact]
    public void ParsesBossHpTagColors()
    {
        WzData wz = WzData.Parse(new MemoryStream(Encoding.UTF8.GetBytes(BossXml)));
        MobData mob = MobData.FromWz(8800000, wz);

        Assert.Equal(4, mob.TagColor);
        Assert.Equal(6, mob.TagBgColor);
    }

    [Fact]
    public void MobImagePath_IsSevenDigitPadded()
    {
        string path = WzMobProvider.MobImagePath("/wz", 100100);
        Assert.EndsWith(Path.Combine("Mob", "0100100.img.xml"), path);
    }

    [Fact]
    public void InMemoryProvider_ReturnsSeeded()
    {
        var provider = new InMemoryMobProvider(new[] { new MobData { TemplateId = 100100, MaxHp = 50, Exp = 25 } });
        Assert.Equal(25, provider.GetMob(100100)!.Exp);
        Assert.Null(provider.GetMob(999));
    }
}
