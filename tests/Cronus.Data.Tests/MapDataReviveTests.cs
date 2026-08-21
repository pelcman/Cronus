using System.Text;
using Cronus.Data;
using Xunit;

namespace Cronus.Data.Tests;

public class MapDataReviveTests
{
    private static WzData Parse(string xml) => WzData.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    [Fact]
    public void ReviveMap_UsesReturnMap_WhenSet()
    {
        MapData map = MapData.FromWz(100020000, Parse(
            "<imgdir name=\"100020000.img\"><imgdir name=\"info\">" +
            "<int name=\"returnMap\" value=\"100000000\"/></imgdir></imgdir>"));

        Assert.Equal(100000000, map.ReturnMap);
        Assert.Equal(100000000, map.ReviveMap);
    }

    [Fact]
    public void ReviveMap_FallsBackToSelf_WhenReturnMapMissing()
    {
        MapData map = MapData.FromWz(100000000, Parse(
            "<imgdir name=\"100000000.img\"><imgdir name=\"info\"/></imgdir>"));

        Assert.Equal(0, map.ReturnMap);
        Assert.Equal(100000000, map.ReviveMap); // 0 → revive in place
    }

    [Fact]
    public void ReviveMap_FallsBackToSelf_WhenReturnMapIsTheNoLinkSentinel()
    {
        MapData map = MapData.FromWz(100000000, Parse(
            "<imgdir name=\"100000000.img\"><imgdir name=\"info\">" +
            "<int name=\"returnMap\" value=\"999999999\"/></imgdir></imgdir>"));

        Assert.Equal(100000000, map.ReviveMap); // NoLink → revive in place
    }
}
