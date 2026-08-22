using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

/// <summary>
/// The /dbgshop catalog: equips split by their String.wz sub-category (Face and Hair excluded —
/// they are looks, not items), the flat tabs whole, ids ascending and de-duplicated.
/// </summary>
public class ItemCatalogTests
{
    private static string WriteTree()
    {
        string root = Path.Combine(Path.GetTempPath(), "cronus-catalog-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "String"));

        File.WriteAllText(Path.Combine(root, "String", "Eqp.img.xml"),
            """
            <imgdir name="Eqp.img">
              <imgdir name="Eqp">
                <imgdir name="Cap">
                  <imgdir name="1002140"><string name="name" value="かぶと"/></imgdir>
                  <imgdir name="1002000"><string name="name" value="ぼうし"/></imgdir>
                </imgdir>
                <imgdir name="Weapon">
                  <imgdir name="1302000"><string name="name" value="つるぎ"/></imgdir>
                </imgdir>
                <imgdir name="Hair">
                  <imgdir name="30000"><string name="name" value="かみ"/></imgdir>
                </imgdir>
                <imgdir name="Face">
                  <imgdir name="20000"><string name="name" value="かお"/></imgdir>
                </imgdir>
              </imgdir>
            </imgdir>
            """);

        File.WriteAllText(Path.Combine(root, "String", "Consume.img.xml"),
            """
            <imgdir name="Consume.img">
              <imgdir name="2000000"><string name="name" value="赤いポーション"/></imgdir>
              <imgdir name="2000001"><string name="name" value="青いポーション"/></imgdir>
            </imgdir>
            """);

        // The item DATA the client would render. 1002140 (cap) and 2000001 (potion) are named
        // above but deliberately absent here — String.wz names plenty of ids the data lacks, and
        // listing one crashes the client, so the catalog must drop them.
        Directory.CreateDirectory(Path.Combine(root, "Character", "Cap"));
        File.WriteAllText(Path.Combine(root, "Character", "Cap", "01002000.img.xml"), "<imgdir/>");
        Directory.CreateDirectory(Path.Combine(root, "Character", "Weapon"));
        File.WriteAllText(Path.Combine(root, "Character", "Weapon", "01302000.img.xml"), "<imgdir/>");
        Directory.CreateDirectory(Path.Combine(root, "Item", "Consume"));
        File.WriteAllText(Path.Combine(root, "Item", "Consume", "0200.img.xml"),
            "<imgdir name=\"0200.img\"><imgdir name=\"02000000\"/></imgdir>");

        return root;
    }

    [Fact]
    public void GroupsEquipsBySubCategory_AndSkipsLooks()
    {
        string root = WriteTree();
        try
        {
            var catalog = new WzItemCatalog(root);
            IReadOnlyList<ItemCategory> categories = catalog.Categories;

            ItemCategory caps = Assert.Single(categories, c => c.Key == "Cap");
            Assert.Equal(new[] { 1002000 }, caps.ItemIds);   // 1002140 is named but has no data
            Assert.Equal("帽子", caps.DisplayName);

            Assert.Single(categories, c => c.Key == "Weapon");
            Assert.Equal(new[] { 2000000 }, Assert.Single(categories, c => c.Key == "Consume").ItemIds);

            // Hair/Face are avatar looks (see /beauty) — never inventory items.
            Assert.DoesNotContain(categories, c => c.Key is "Hair" or "Face");

            // Absent tables (Ins/Etc/Cash/Pet here) simply do not appear.
            Assert.DoesNotContain(categories, c => c.ItemIds.Count == 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NamedItemsWithoutData_AreExcluded()
    {
        string root = WriteTree();
        try
        {
            var all = new WzItemCatalog(root).Categories.SelectMany(c => c.ItemIds).ToHashSet();

            Assert.Contains(1002000, all);      // named + data
            Assert.DoesNotContain(1002140, all); // named, no data — would crash the client
            Assert.DoesNotContain(2000001, all);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingWzTree_YieldsAnEmptyCatalog()
    {
        var catalog = new WzItemCatalog(Path.Combine(Path.GetTempPath(), "cronus-no-such-" + Guid.NewGuid()));
        Assert.Empty(catalog.Categories);
    }
}
