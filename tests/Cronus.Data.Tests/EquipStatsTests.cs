using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

public class EquipStatsTests
{
    [Fact]
    public void InMemoryProvider_ReturnsSeededEquipStats()
    {
        var provider = new InMemoryItemProvider(
            new[] { new ConsumeSpec { ItemId = 2000000, Hp = 50 } },
            equips: new Dictionary<int, EquipStats>
            {
                [1302000] = new EquipStats { UpgradeSlots = 7, Watk = 17, Str = 3 },
            });

        EquipStats? sword = provider.GetEquipStats(1302000);
        Assert.NotNull(sword);
        Assert.Equal((byte)7, sword!.UpgradeSlots);
        Assert.Equal((short)17, sword.Watk);
        Assert.Equal((short)3, sword.Str);
        // Unseeded stat keys default to 0.
        Assert.Equal((short)0, sword.Matk);
    }

    [Fact]
    public void InMemoryProvider_NonEquipId_EquipStatsIsNull()
    {
        var provider = new InMemoryItemProvider(
            new[] { new ConsumeSpec { ItemId = 2000000, Hp = 50 } },
            equips: new Dictionary<int, EquipStats>
            {
                [1302000] = new EquipStats { Watk = 17 },
            });

        // A consumable id (category 2) is not an equip.
        Assert.Null(provider.GetEquipStats(2000000));
    }

    [Fact]
    public void InMemoryProvider_UnseededEquipId_EquipStatsIsNull()
    {
        var provider = new InMemoryItemProvider(
            new[] { new ConsumeSpec { ItemId = 2000000, Hp = 50 } },
            equips: new Dictionary<int, EquipStats>
            {
                [1302000] = new EquipStats { Watk = 17 },
            });

        Assert.Null(provider.GetEquipStats(1302001));
    }

    [Fact]
    public void InMemoryProvider_WithoutSeededEquips_EquipStatsIsNull()
    {
        // The equips argument is optional, so the original constructor shapes keep working.
        var provider = new InMemoryItemProvider(new[] { new ConsumeSpec { ItemId = 2000000, Hp = 50 } });

        Assert.Null(provider.GetEquipStats(1302000));
    }
}
