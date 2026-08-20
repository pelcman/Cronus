using System.Text;
using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

public class WzMapDataTests
{
    // A trimmed Map .img in the wz_xml format: two portals, one linking to another map.
    private const string MapXml = """
        <imgdir name="100000000.img">
          <imgdir name="info">
            <int name="returnMap" value="100000000"/>
          </imgdir>
          <imgdir name="portal">
            <imgdir name="0">
              <string name="pn" value="sp"/>
              <int name="pt" value="0"/>
              <int name="x" value="-10"/>
              <int name="y" value="33"/>
              <int name="tm" value="999999999"/>
              <string name="tn" value=""/>
            </imgdir>
            <imgdir name="1">
              <string name="pn" value="east00"/>
              <int name="pt" value="2"/>
              <int name="x" value="500"/>
              <int name="y" value="120"/>
              <int name="tm" value="104040000"/>
              <string name="tn" value="west00"/>
            </imgdir>
          </imgdir>
          <imgdir name="life">
            <imgdir name="0">
              <string name="type" value="n"/>
              <int name="id" value="9010000"/>
              <int name="x" value="120"/>
              <int name="y" value="-60"/>
              <int name="fh" value="7"/>
              <int name="f" value="1"/>
              <int name="rx0" value="100"/>
              <int name="rx1" value="140"/>
            </imgdir>
            <imgdir name="1">
              <string name="type" value="m"/>
              <int name="id" value="100100"/>
              <int name="x" value="300"/>
              <int name="y" value="0"/>
            </imgdir>
          </imgdir>
        </imgdir>
        """;

    private static WzData Parse(string xml)
        => WzData.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    [Fact]
    public void ParsesPortals()
    {
        MapData map = MapData.FromWz(100000000, Parse(MapXml));

        Assert.Equal(2, map.Portals.Count);

        PortalData spawn = map.SpawnPortal!;
        Assert.Equal("sp", spawn.Name);
        Assert.Equal(-10, spawn.X);
        Assert.Equal(33, spawn.Y);
        Assert.False(spawn.LinksToMap);

        PortalData east = map.FindPortal("east00")!;
        Assert.Equal(104040000, east.TargetMapId);
        Assert.Equal("west00", east.TargetName);
        Assert.True(east.LinksToMap);
    }

    [Fact]
    public void ParsesNpcAndMobLife()
    {
        MapData map = MapData.FromWz(100000000, Parse(MapXml));

        NpcSpawn npc = Assert.Single(map.Npcs);
        Assert.Equal(9010000, npc.TemplateId);
        Assert.Equal(120, npc.X);
        Assert.Equal(-60, npc.Y);
        Assert.Equal(7, npc.Foothold);
        Assert.Equal(0, npc.Facing); // wz f=1 -> packet 0
        Assert.Equal(100, npc.Rx0);
        Assert.Equal(140, npc.Rx1);
        Assert.False(npc.Hidden);

        MobSpawn mob = Assert.Single(map.Mobs);
        Assert.Equal(100100, mob.TemplateId);
        Assert.Equal(300, mob.X);
        Assert.Equal(0, mob.Y);
    }

    [Fact]
    public void WzData_ResolvesPathsAndTypedValues()
    {
        WzData root = Parse(MapXml);
        Assert.Equal(100000000, root.GetInt("info/returnMap"));
        Assert.Equal("sp", root.GetString("portal/0/pn"));
        Assert.Null(root.Resolve("portal/9"));
    }

    [Fact]
    public void MapImagePath_FollowsUpstreamConvention()
    {
        string path = WzMapProvider.MapImagePath("/wz", 104040000);
        Assert.EndsWith(Path.Combine("Map", "Map1", "104040000.img.xml"), path);
    }

    [Fact]
    public void InMemoryProvider_ReturnsSeededMaps()
    {
        MapData map = MapData.FromWz(100000000, Parse(MapXml));
        var provider = new InMemoryMapProvider(new[] { map });

        Assert.Same(map, provider.GetMap(100000000));
        Assert.Null(provider.GetMap(222));
    }
}
