using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class PortalScriptTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private const int DungeonMap = 200000000;

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    /// <summary>Enters, steps on a scripted portal, and signals when the warp (a 2nd SetField) lands.</summary>
    private sealed class PortalStepper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _portalName;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private int _setFields;

        public PortalStepper(int characterId, string portalName)
        {
            _characterId = characterId;
            _portalName = portalName;
        }

        public TaskCompletionSource Warped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode != _opSetField)
            {
                return;
            }

            if (++_setFields == 1)
            {
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserPortalScriptRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(0);            // portal count
                w.WriteString(_portalName);
                w.WriteShort(0);           // x
                w.WriteShort(0);           // y
                await session.SendAsync(w.ToArray());
            }
            else
            {
                Warped.TrySetResult();     // the portal script warped us
            }
        }
    }

    [Fact]
    public async Task ScriptedPortal_RunsScript_AndWarps()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Explorer", MapId = 100000000 });

        var startMap = new MapData
        {
            MapId = 100000000,
            Portals = new[]
            {
                new PortalData { Id = 1, Type = 2, Name = "dungeon", Script = "enterDungeon", X = 0, Y = 0 },
            },
        };
        var maps = new InMemoryMapProvider(new[]
        {
            startMap,
            new MapData { MapId = DungeonMap, Portals = Array.Empty<PortalData>() },
        });
        var fields = new FieldRegistry(maps);
        var portalScripts = new PortalScriptEngine(new DictionaryPortalScriptSource(
            new Dictionary<string, string> { ["enterDungeon"] = "function start() { player.warp(200000000); }" }));

        using var cts = new CancellationTokenSource(Timeout);

        var client = new PortalStepper(hero.Id, "dungeon");
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, portalScripts: portalScripts);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.Warped.Task.WaitAsync(cts.Token);
        Assert.Equal(DungeonMap, hero.MapId); // the portal script moved the character
    }

    [Fact]
    public async Task PortalWithoutScript_DoesNothing()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Explorer", MapId = 100000000 });

        var startMap = new MapData
        {
            MapId = 100000000,
            Portals = new[] { new PortalData { Id = 1, Type = 2, Name = "plain", X = 0, Y = 0 } },
        };
        var maps = new InMemoryMapProvider(new[] { startMap });
        var fields = new FieldRegistry(maps);
        var portalScripts = new PortalScriptEngine(new DictionaryPortalScriptSource(new Dictionary<string, string>()));

        using var cts = new CancellationTokenSource(Timeout);

        var client = new PortalStepper(hero.Id, "plain");
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, portalScripts: portalScripts);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        // No warp: the character stays put (give the request time to be a no-op).
        await Task.Delay(200);
        Assert.Equal(100000000, hero.MapId);
        Assert.False(client.Warped.Task.IsCompleted);
    }
}
