using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

/// <summary>
/// The /dbgwarp catalog: String.wz map names grouped by region, in menu order, intersected with
/// the maps that really have field data (a named-but-dataless id would leave the client with
/// nothing to draw — the same trap <see cref="ItemCatalogTests"/> covers for items).
/// </summary>
public class MapCatalogTests
{
    private static string WriteTree()
    {
        string root = Path.Combine(Path.GetTempPath(), "cronus-mapcatalog-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "String"));

        File.WriteAllText(Path.Combine(root, "String", "Map.img.xml"),
            """
            <imgdir name="Map.img">
              <imgdir name="maple">
                <imgdir name="0"><string name="streetName" value="メイプル road"/><string name="mapName" value="はじまりの島"/></imgdir>
              </imgdir>
              <imgdir name="victoria">
                <imgdir name="100000001"><string name="streetName" value="ヘネシス"/><string name="mapName" value="民家"/></imgdir>
                <imgdir name="100000000"><string name="streetName" value="ヘネシス"/><string name="mapName" value="ヘネシス"/></imgdir>
                <imgdir name="104040000"><string name="streetName" value="連なる木の道"/><string name="mapName" value="きのこの森"/></imgdir>
              </imgdir>
              <imgdir name="ossyria">
                <imgdir name="211000000"><string name="streetName" value="オルビス"/><string name="mapName" value="オルビス"/></imgdir>
              </imgdir>
            </imgdir>
            """);

        // The field DATA the client would load. 100000001 and 211000000 are named above but have
        // no file, so the catalog must drop them.
        Directory.CreateDirectory(Path.Combine(root, "Map", "Map0"));
        File.WriteAllText(Path.Combine(root, "Map", "Map0", "000000000.img.xml"), "<imgdir/>");
        Directory.CreateDirectory(Path.Combine(root, "Map", "Map1"));
        File.WriteAllText(Path.Combine(root, "Map", "Map1", "100000000.img.xml"), "<imgdir/>");
        File.WriteAllText(Path.Combine(root, "Map", "Map1", "104040000.img.xml"), "<imgdir/>");

        return root;
    }

    [Fact]
    public void GroupsMapsByRegion_InIdOrder()
    {
        string root = WriteTree();
        try
        {
            IReadOnlyList<MapRegion> regions = new WzMapCatalog(root).Regions;

            // Menu order is the declared region order, not the file order.
            Assert.Equal(new[] { "maple", "victoria" }, regions.Select(r => r.Key));
            Assert.Equal("ビクトリアアイランド", regions[1].DisplayName);

            Assert.Equal(new[] { 100000000, 104040000 }, regions[1].Maps.Select(m => m.MapId));
            Assert.Equal(2, regions[1].MapCount);

            // The two surviving maps sit on different streets, ordered by lowest map id.
            Assert.Equal(new[] { "ヘネシス", "連なる木の道" }, regions[1].Streets.Select(s => s.Name));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NamedMapsWithoutFieldData_AreExcluded()
    {
        string root = WriteTree();
        try
        {
            var all = new WzMapCatalog(root).Regions.SelectMany(r => r.Maps).Select(m => m.MapId).ToHashSet();

            Assert.Contains(100000000, all);       // named + data
            Assert.DoesNotContain(100000001, all); // named, no field data
            Assert.DoesNotContain(211000000, all); // whole region drops out
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DisplayName_CombinesStreetAndMap_ButNotWhenEqual()
    {
        string root = WriteTree();
        try
        {
            List<MapEntry> victoria = new WzMapCatalog(root).Regions.Single(r => r.Key == "victoria").Maps.ToList();

            Assert.Equal("ヘネシス", victoria[0].DisplayName);                    // street == map
            Assert.Equal("連なる木の道 : きのこの森", victoria[1].DisplayName);   // street != map
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingWzTree_YieldsAnEmptyCatalog()
        => Assert.Empty(new WzMapCatalog(Path.Combine(Path.GetTempPath(), "cronus-no-such-" + Guid.NewGuid())).Regions);
}
