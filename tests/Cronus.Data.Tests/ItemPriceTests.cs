using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

public class ItemPriceTests
{
    [Fact]
    public void InMemoryProvider_ReturnsSeededPrice()
    {
        var provider = new InMemoryItemProvider(
            new[] { new ConsumeSpec { ItemId = 2000000, Hp = 50 } },
            new Dictionary<int, int>
            {
                [2000000] = 25,
                [4000019] = 2,
            });

        Assert.Equal(25, provider.GetPrice(2000000));
        Assert.Equal(2, provider.GetPrice(4000019));
    }

    [Fact]
    public void InMemoryProvider_UnknownItem_PriceIsNull()
    {
        var provider = new InMemoryItemProvider(
            new[] { new ConsumeSpec { ItemId = 2000000, Hp = 50 } },
            new Dictionary<int, int> { [2000000] = 25 });

        Assert.Null(provider.GetPrice(9999999));
    }

    [Fact]
    public void InMemoryProvider_WithoutSeededPrices_PriceIsNull()
    {
        // The prices argument is optional, so the original constructor shape keeps working.
        var provider = new InMemoryItemProvider(new[] { new ConsumeSpec { ItemId = 2000000, Hp = 50 } });

        Assert.Null(provider.GetPrice(2000000));
    }
}
