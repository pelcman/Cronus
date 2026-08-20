using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

/// <summary>Loads the bundled sample wz tree through the real provider + path convention.</summary>
public class SampleWzTests
{
    private static string SampleRoot => Path.Combine(AppContext.BaseDirectory, "sample-wz");

    [Fact]
    public void WzMapProvider_LoadsBundledSampleMap()
    {
        var provider = new WzMapProvider(SampleRoot);

        MapData? map = provider.GetMap(100000000);

        Assert.NotNull(map);
        Assert.NotNull(map!.SpawnPortal);
        NpcSpawn npc = Assert.Single(map.Npcs);
        Assert.Equal(9010000, npc.TemplateId); // matches scripts/npc/9010000.js
    }

    [Fact]
    public void MapImagePath_MatchesSampleLayout()
    {
        string path = WzMapProvider.MapImagePath(SampleRoot, 100000000);
        Assert.True(File.Exists(path), $"expected sample map at {path}");
    }
}
