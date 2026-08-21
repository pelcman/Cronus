using Cronus.Domain;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class PlayerRegenTests
{
    [Fact]
    public void Apply_BelowMax_RegensHpAndMp_AndReportsChange()
    {
        var c = new Character { Name = "Hurt", Hp = 10, MaxHp = 500, Mp = 10, MaxMp = 500 };

        StatFlag changed = PlayerRegen.Apply(c);

        Assert.True(changed.HasFlag(StatFlag.Hp));
        Assert.True(changed.HasFlag(StatFlag.Mp));
        Assert.Equal(20, c.Hp); // +max(3, 500/50)=10
        Assert.Equal(20, c.Mp);
    }

    [Fact]
    public void Apply_NearMax_ClampsToMax()
    {
        var c = new Character { Name = "Almost", Hp = 49, MaxHp = 50, Mp = 4, MaxMp = 5 };

        StatFlag changed = PlayerRegen.Apply(c);

        Assert.Equal(50, c.Hp); // clamped, not 49+3
        Assert.Equal(5, c.Mp);
        Assert.True(changed.HasFlag(StatFlag.Hp));
        Assert.True(changed.HasFlag(StatFlag.Mp));
    }

    [Fact]
    public void Apply_AtFull_ChangesNothing()
    {
        var c = new Character { Name = "Full", Hp = 50, MaxHp = 50, Mp = 5, MaxMp = 5 };

        StatFlag changed = PlayerRegen.Apply(c);

        Assert.Equal((StatFlag)0, changed);
        Assert.Equal(50, c.Hp);
        Assert.Equal(5, c.Mp);
    }

    [Fact]
    public void Apply_OnlyHpMissing_ReportsOnlyHp()
    {
        var c = new Character { Name = "HpOnly", Hp = 40, MaxHp = 50, Mp = 5, MaxMp = 5 };

        StatFlag changed = PlayerRegen.Apply(c);

        Assert.True(changed.HasFlag(StatFlag.Hp));
        Assert.False(changed.HasFlag(StatFlag.Mp));
    }

    [Fact]
    public void Apply_Seated_RecoversThreeTimesFaster()
    {
        var standing = new Character { Name = "Stand", Hp = 0, MaxHp = 500, Mp = 0, MaxMp = 500 };
        var sitting = new Character { Name = "Sit", Hp = 0, MaxHp = 500, Mp = 0, MaxMp = 500 };

        PlayerRegen.Apply(standing, seated: false);
        PlayerRegen.Apply(sitting, seated: true);

        Assert.Equal(10, standing.Hp); // max(3, 500/50) = 10
        Assert.Equal(30, sitting.Hp);  // x3 while seated
        Assert.Equal(30, sitting.Mp);
    }
}
