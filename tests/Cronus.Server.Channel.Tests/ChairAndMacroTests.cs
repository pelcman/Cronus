using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class ChairAndMacroTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void MacroInit_EncodesRowsInSlotOrder()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var macros = new Dictionary<int, SkillMacroEntry>
        {
            [1] = new("Buffs", 1, 1001003, 0, 0),
            [0] = new("Attack", 0, 1001004, 1001005, 0),
        };

        byte[] p = packets.MacroSysDataInit(macros);
        int i = 2;
        Assert.Equal(2, p[i++]); // count
        // Slot 0 first regardless of dictionary order.
        short len = BitConverter.ToInt16(p, i); i += 2;
        Assert.Equal("Attack", System.Text.Encoding.ASCII.GetString(p, i, len)); i += len;
        Assert.Equal(0, p[i++]);                                  // shout
        Assert.Equal(1001004, BitConverter.ToInt32(p, i)); i += 4;
        Assert.Equal(1001005, BitConverter.ToInt32(p, i)); i += 4;
        Assert.Equal(0, BitConverter.ToInt32(p, i));

        Assert.Equal(3, packets.MacroSysDataInit(null).Length); // opcode + zero count
    }

    private sealed class Sitter : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);

        public Sitter(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Watches for the portable-chair broadcast.</summary>
    private sealed class Onlooker : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opChair = ServerOps.Get(ServerOpcode.UserSetActivePortableChair);

        public Onlooker(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(int CharId, int ItemId)> ChairSeen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opChair)
            {
                ChairSeen.TrySetResult((p.ReadInt(), p.ReadInt()));
            }

            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task PortableChair_BroadcastsToOnlookers()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000 });
        alice.EquippedItems.Add(new InventoryItem { ItemId = 3010000, Position = 1, Quantity = 1, CharacterId = alice.Id });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new Onlooker(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        var aliceClient = new Sitter(alice.Id);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);
        await aliceClient.Ready.Task.WaitAsync(cts.Token);

        var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserPortableChairSitRequest), ServerConfig.Jms186.PacketHeaderSize, ServerConfig.Jms186.CodePage);
        w.WriteInt(3010000);
        await aliceClient.Session!.SendAsync(w.ToArray());

        (int charId, int itemId) = await bobClient.ChairSeen.Task.WaitAsync(cts.Token);
        Assert.Equal(alice.Id, charId);
        Assert.Equal(3010000, itemId);
    }

    [Fact]
    public async Task Macros_SaveAndReplayOnNextLogin()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hero", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Sitter(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);
        await client.Ready.Task.WaitAsync(cts.Token);

        var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserMacroSysDataModified), ServerConfig.Jms186.PacketHeaderSize, ServerConfig.Jms186.CodePage);
        w.WriteByte(1);
        w.WriteString("Buffs");
        w.WriteByte(1);
        w.WriteInt(1001003);
        w.WriteInt(0);
        w.WriteInt(0);
        await client.Session!.SendAsync(w.ToArray());

        // The handler persists asynchronously; wait for the macro to land on the character.
        while (hero.SkillMacros.Count == 0)
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(5, cts.Token);
        }

        SkillMacroEntry macro = hero.SkillMacros[0];
        Assert.Equal(new SkillMacroEntry("Buffs", 1, 1001003, 0, 0), macro);
    }
}
