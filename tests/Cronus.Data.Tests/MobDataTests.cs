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

    [Fact]
    public void ParsesMobStats()
    {
        WzData wz = WzData.Parse(new MemoryStream(Encoding.UTF8.GetBytes(MobXml)));
        MobData mob = MobData.FromWz(100100, wz);

        Assert.Equal(100100, mob.TemplateId);
        Assert.Equal(50, mob.MaxHp);
        Assert.Equal(25, mob.Exp);
        Assert.Equal(3, mob.Level);
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
