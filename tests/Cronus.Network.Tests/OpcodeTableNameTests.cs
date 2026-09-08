using Cronus.Network.Packets;
using Xunit;

namespace Cronus.Network.Tests;

/// <summary>The reverse lookup that names packets no handler claims (the [dev] unhandled log).</summary>
public class OpcodeTableNameTests
{
    private static string OpcodeDir => Path.Combine(AppContext.BaseDirectory, "opcodes");

    [Fact]
    public void NameOf_RoundTripsEveryDefinedValue()
    {
        OpcodeTable table = OpcodeTable.LoadFile(Path.Combine(OpcodeDir, "JMS_v186_ClientPacket.properties"));
        foreach ((string name, int value) in table.Entries)
        {
            string? back = table.NameOf(value);
            Assert.NotNull(back);
            Assert.Equal(value, table.Get(back!));       // the same value, even when names share one
        }

        Assert.Equal("CP_UserPortalScrollUseRequest", table.NameOf(table.Get("CP_UserPortalScrollUseRequest")));
        Assert.Null(table.NameOf(OpcodeTable.Undefined));
        Assert.Null(table.NameOf(0x7FFF));
    }
}
