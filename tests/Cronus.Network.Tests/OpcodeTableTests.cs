using Cronus.Network.Packets;
using Xunit;

namespace Cronus.Network.Tests;

public class OpcodeTableTests
{
    private static string OpcodeDir => Path.Combine(AppContext.BaseDirectory, "opcodes");

    [Fact]
    public void LoadsJmsV186ClientOpcodes()
    {
        var table = OpcodeTable.LoadFile(Path.Combine(OpcodeDir, "JMS_v186_ClientPacket.properties"));

        Assert.Equal(0x0001, table[ClientOpcode.CheckPassword]);
        Assert.Equal(0x0003, table[ClientOpcode.WorldInfoRequest]);
        Assert.Equal(0x0006, table[ClientOpcode.SelectCharacter]);
        Assert.Equal(0x000A, table[ClientOpcode.ViewAllChar]);
    }

    [Fact]
    public void LoadsJmsV186ServerOpcodes()
    {
        var table = OpcodeTable.LoadFile(Path.Combine(OpcodeDir, "JMS_v186_ServerPacket.properties"));

        Assert.Equal(0x0000, table[ServerOpcode.CheckPasswordResult]);
        Assert.Equal(0x0002, table[ServerOpcode.WorldInformation]);
        Assert.Equal(0x0008, table[ServerOpcode.MigrateCommand]);
        Assert.Equal(0x0009, table[ServerOpcode.AliveReq]);
        Assert.Equal(0x0014, table[ServerOpcode.ViewAllCharResult]);
    }

    [Fact]
    public void SectionMarkersAreUndefined()
    {
        var table = OpcodeTable.LoadFile(Path.Combine(OpcodeDir, "JMS_v186_ClientPacket.properties"));

        Assert.False(table.IsDefined("CP_BEGIN_SOCKET"));
        Assert.Equal(OpcodeTable.Undefined, table["CP_BEGIN_SOCKET"]);
        Assert.Equal(OpcodeTable.Undefined, table["NAME_THAT_DOES_NOT_EXIST"]);
    }

    [Fact]
    public void ResolvesHexDecimalAndRelativeForms()
    {
        string[] lines =
        {
            "# comment line",
            "BASE = @0010",
            "DECIMAL = 32",
            "REL_PLUS = BASE + 3",
            "REL_MINUS = BASE - 1",
            "CHAINED = REL_PLUS + 1",
            "MARKER",
        };

        var table = OpcodeTable.Load(lines);

        Assert.Equal(0x10, table["BASE"]);
        Assert.Equal(32, table["DECIMAL"]);
        Assert.Equal(0x13, table["REL_PLUS"]);
        Assert.Equal(0x0F, table["REL_MINUS"]);
        Assert.Equal(0x14, table["CHAINED"]);
        Assert.False(table.IsDefined("MARKER"));
    }

    [Fact]
    public void JmsV186FilesHaveNoUnresolvedNames()
    {
        var client = OpcodeTable.LoadFile(Path.Combine(OpcodeDir, "JMS_v186_ClientPacket.properties"));
        var server = OpcodeTable.LoadFile(Path.Combine(OpcodeDir, "JMS_v186_ServerPacket.properties"));

        Assert.Empty(client.UnresolvedNames);
        Assert.Empty(server.UnresolvedNames);
    }
}
