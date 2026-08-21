using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

public class DropDataTests
{
    private const string Sql = """
        INSERT INTO `drop_data` (`id`, `dropperid`, `itemid`, `minimum_quantity`, `maximum_quantity`, `questid`, `chance`) VALUES
        (39043, 8170000, 4001107, 1, 1, 0, 50),
        (1, 100100, 0, 8, 12, 0, 800),
        (2, 100100, 4000019, 1, 1, 0, 400),
        (3, 100100, 2000000, 1, 3, 0, 100);
        """;

    [Fact]
    public void Parse_GroupsRowsByDropper()
    {
        SqlDropProvider provider = SqlDropProvider.Parse(Sql);

        IReadOnlyList<DropEntry> snail = provider.GetDrops(100100);
        Assert.Equal(3, snail.Count);

        // The meso row (itemid 0) keeps its quantity range and chance.
        DropEntry meso = Assert.Single(snail, e => e.ItemId == 0);
        Assert.Equal(8, meso.MinQuantity);
        Assert.Equal(12, meso.MaxQuantity);
        Assert.Equal(800, meso.Chance);

        // An item row parses its fields in column order.
        DropEntry potion = Assert.Single(snail, e => e.ItemId == 2000000);
        Assert.Equal(1, potion.MinQuantity);
        Assert.Equal(3, potion.MaxQuantity);
        Assert.Equal(100, potion.Chance);
    }

    [Fact]
    public void Parse_SeparateDroppersAreDistinct()
    {
        SqlDropProvider provider = SqlDropProvider.Parse(Sql);

        DropEntry only = Assert.Single(provider.GetDrops(8170000));
        Assert.Equal(4001107, only.ItemId);
        Assert.Equal(50, only.Chance);
    }

    [Fact]
    public void GetDrops_UnknownDropper_IsEmpty()
    {
        SqlDropProvider provider = SqlDropProvider.Parse(Sql);
        Assert.Empty(provider.GetDrops(999999));
    }

    [Fact]
    public void LoadFile_Missing_YieldsEmptyProvider()
    {
        SqlDropProvider provider = SqlDropProvider.LoadFile(Path.Combine(Path.GetTempPath(), "no-such-drop-data.sql"));
        Assert.Empty(provider.GetDrops(100100));
    }

    [Fact]
    public void InMemoryProvider_ReturnsSeededDrops()
    {
        var provider = new InMemoryDropProvider(new Dictionary<int, IReadOnlyList<DropEntry>>
        {
            [100100] = new[] { new DropEntry(2000000, 1, 1, 0, 500) },
        });

        DropEntry entry = Assert.Single(provider.GetDrops(100100));
        Assert.Equal(2000000, entry.ItemId);
        Assert.Empty(provider.GetDrops(100101));
    }
}
