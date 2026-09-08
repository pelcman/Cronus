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

/// <summary>
/// The never-silence rule for packets no handler claims (2026-09-08): an item-use request the
/// server does not implement still frees the client's inventory lock and tells the player, once,
/// that the feature is not there. And the /sweep crash inventory walks every catalogued map,
/// logging each one before the warp.
/// </summary>
public class UnhandledPacketTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class ProbeClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opInventoryOperation = ServerOps.Get(ServerOpcode.InventoryOperation);
        private readonly int _opUserChat = ServerOps.Get(ServerOpcode.UserChat);
        private int _setFields;

        public ProbeClient(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }

        public TaskCompletionSource<bool> EnteredGame { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(byte Unlock, byte Count)> InventoryOp { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> DevLine { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<int> MapChanges { get; } = new();
        public int ChatLines { get; private set; }

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
                _setFields++;
                if (_setFields == 1)
                {
                    EnteredGame.TrySetResult(true);
                    return ValueTask.CompletedTask;
                }

                p.ReadShort();               // ClientOptMan
                p.ReadInt();                 // channel
                p.ReadByte();
                p.ReadInt();                 // old driver id
                p.ReadByte();                // portal count
                p.ReadByte();                // bCharacterData = false
                p.ReadShort();               // notifier
                p.ReadByte();                // clear stat
                lock (MapChanges)
                {
                    MapChanges.Add(p.ReadInt());
                }
            }
            else if (opcode == _opInventoryOperation)
            {
                InventoryOp.TrySetResult((p.ReadByte(), p.ReadByte()));
            }
            else if (opcode == _opUserChat)
            {
                p.ReadInt();                 // character id
                p.ReadByte();                // gm flag
                string text = p.ReadString();
                ChatLines++;
                if (text.Contains("[DEV]", StringComparison.Ordinal))
                {
                    DevLine.TrySetResult(text);
                }
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>Sends an opcode the server has no handler for, with a few junk bytes.</summary>
        public async ValueTask SendRawAsync(string opcodeName)
        {
            var w = new PacketWriter(ClientOps.Get(opcodeName), Session!.Config.PacketHeaderSize, Session.Config.CodePage);
            w.WriteInt(0);
            w.WriteShort(1);
            w.WriteInt(2020000);
            await Session.SendAsync(w.ToArray());
        }

        public async ValueTask ChatAsync(string message)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), Session!.Config.PacketHeaderSize, Session.Config.CodePage);
            w.WriteInt(0);
            w.WriteString(message);
            w.WriteByte(0);
            await Session.SendAsync(w.ToArray());
        }
    }

    private sealed class ThreeMapCatalog : IMapCatalog
    {
        public IReadOnlyList<MapRegion> Regions { get; } = new[]
        {
            new MapRegion("victoria", "ビクトリア", new[]
            {
                new MapStreet("ヘネシス", new[]
                {
                    new MapEntry(100000000, "ヘネシス", "ヘネシス"),
                    new MapEntry(100000001, "ヘネシス", "市場"),
                    new MapEntry(100000002, "ヘネシス", "狩場"),
                }),
            }),
        };
    }

    private static (MapleSession Server, MapleSession Client) Wire(ProbeClient client, ChannelHandler handler, CancellationToken ct)
    {
        var config = ServerConfig.Jms186;
        var c2s = new Pipe();
        var s2c = new Pipe();
        var server = new MapleSession(c2s.Reader, s2c.Writer, config, SessionRole.Server, handler);
        var clientSession = new MapleSession(s2c.Reader, c2s.Writer, config, SessionRole.Client, client);
        _ = server.RunAsync(ct);
        _ = clientSession.RunAsync(ct);
        return (server, clientSession);
    }

    [Fact]
    public async Task UnimplementedItemUseRequest_UnlocksTheInventory_AndSaysDevOnce()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Prober", MapId = 100000000 });
        var maps = new InMemoryMapProvider(new[] { new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() } });
        var client = new ProbeClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, new FieldRegistry(maps), maps);

        using var cts = new CancellationTokenSource(Timeout);
        (MapleSession server, MapleSession clientSession) = Wire(client, handler, cts.Token);
        await using MapleSession s1 = server;
        await using MapleSession s2 = clientSession;
        await client.EnteredGame.Task.WaitAsync(cts.Token);

        // A mastery book: the client locks its inventory until the server answers.
        await client.SendRawAsync("CP_UserSkillLearnItemUseRequest");

        (byte unlock, byte count) = await client.InventoryOp.Task.WaitAsync(cts.Token);
        Assert.Equal(1, unlock);                                  // the empty InventoryOperation = unlock
        Assert.Equal(0, count);
        string line = await client.DevLine.Task.WaitAsync(cts.Token);
        Assert.Contains("CP_UserSkillLearnItemUseRequest", line);
        Assert.StartsWith("[DEV]", line);

        // The same opcode again: still unlocked, but no second [DEV] line.
        int chatLines = client.ChatLines;
        await client.SendRawAsync("CP_UserSkillLearnItemUseRequest");
        await Task.Delay(300, cts.Token);
        Assert.Equal(chatLines, client.ChatLines);
    }

    [Fact]
    public async Task Sweep_WarpsThroughEveryCataloguedMap_LoggingEachBeforeTheWarp()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Sweeper", MapId = 100000000 });
        var maps = new InMemoryMapProvider(new[]
        {
            new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() },
            new MapData { MapId = 100000001, Portals = Array.Empty<PortalData>() },
            new MapData { MapId = 100000002, Portals = Array.Empty<PortalData>() },
        });
        var client = new ProbeClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, new FieldRegistry(maps), maps, mapCatalog: new ThreeMapCatalog());
        if (File.Exists(ChannelHandler.SweepProgressFile))
        {
            File.Delete(ChannelHandler.SweepProgressFile);
        }

        using var cts = new CancellationTokenSource(Timeout);
        (MapleSession server, MapleSession clientSession) = Wire(client, handler, cts.Token);
        await using MapleSession s1 = server;
        await using MapleSession s2 = clientSession;
        await client.EnteredGame.Task.WaitAsync(cts.Token);

        await client.ChatAsync("/sweep maps 0 999999999 0.05");

        while (true)
        {
            lock (client.MapChanges)
            {
                if (client.MapChanges.Count >= 3)
                {
                    break;
                }
            }

            await Task.Delay(20, cts.Token);
        }

        lock (client.MapChanges)
        {
            Assert.Equal(new[] { 100000000, 100000001, 100000002 }, client.MapChanges.Take(3));
        }

        // Every map was logged before its warp; a crash would leave the culprit on the last line.
        await Task.Delay(200, cts.Token);
        string[] lines = File.ReadAllLines(ChannelHandler.SweepProgressFile);
        Assert.Contains(lines, l => l.StartsWith("100000000\t", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("100000002\t狩場", StringComparison.Ordinal) || l.StartsWith("100000002\tヘネシス : 狩場", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("# done", StringComparison.Ordinal));
    }
}
