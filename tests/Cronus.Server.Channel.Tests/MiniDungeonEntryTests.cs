using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// A script's <c>player.enterMiniDungeon</c> warps the first comer into the dungeon map (its out00
/// portal) and refuses a stranger while the room is held — the refusal reaches the client as the
/// script's [Notice] line. Party members inside admit the rest (unit-tested on the admit rule).
/// </summary>
public class MiniDungeonEntryTests
{
    private const int Entrance = 100020000;
    private const int Dungeon = 100020100;
    private const int Npc = 9000010;

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class Bot : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opBroadcast = ServerOps.Get(ServerOpcode.BroadcastMsg);

        public Bot(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public int SetFields { get; private set; }
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Moved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Notices { get; } = new();
        public TaskCompletionSource<bool> Noticed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                SetFields++;
                if (SetFields == 1)
                {
                    Entered.TrySetResult(true);
                }
                else
                {
                    Moved.TrySetResult(true);
                }
            }
            else if (opcode == _opBroadcast)
            {
                if (p.ReadByte() == 0) // BM_NOTICE
                {
                    Notices.Add(p.ReadString());
                    Noticed.TrySetResult(true);
                }
            }

            return ValueTask.CompletedTask;
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

    private static IMapProvider Maps() => new InMemoryMapProvider(new[]
    {
        new MapData
        {
            MapId = Entrance,
            Portals = new[] { new PortalData { Id = 0, Name = "sp", Type = 0 }, new PortalData { Id = 1, Name = "MD00", Type = 9, Script = "MD_pig" } },
        },
        new MapData
        {
            MapId = Dungeon,
            Portals = new[] { new PortalData { Id = 0, Name = "sp", Type = 0 }, new PortalData { Id = 1, Name = "out00", Type = 9, Script = "MD_pig" } },
        },
    });

    [Fact]
    public async Task FirstComerEnters_StrangerIsRefusedWithANotice()
    {
        var scripts = new NpcScriptEngine(new DictionaryNpcScriptSource(new Dictionary<int, string>
        {
            [Npc] = $"function start() {{ if (!player.enterMiniDungeon({Dungeon})) player.message('[DEV] busy'); }}",
        }));
        var repo = new InMemoryCharacterRepository();
        Character first = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "First", MapId = Entrance, Level = 30, Job = 100 });
        Character second = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Second", MapId = Entrance, Level = 30, Job = 100 });
        IMapProvider maps = Maps();
        var fields = new FieldRegistry(maps);
        var parties = new PartyRegistry();
        var botA = new Bot(first.Id);
        var botB = new Bot(second.Id);
        var handlerA = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, scripts, parties: parties);
        var handlerB = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, scripts, parties: parties);

        var a2s = new Pipe(); var s2a = new Pipe();
        var b2s = new Pipe(); var s2b = new Pipe();
        await using var serverA = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, handlerA);
        await using var clientA = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, botA);
        await using var serverB = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, handlerB);
        await using var clientB = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, botB);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        _ = serverA.RunAsync(cts.Token);
        _ = clientA.RunAsync(cts.Token);
        _ = serverB.RunAsync(cts.Token);
        _ = clientB.RunAsync(cts.Token);
        await botA.Entered.Task.WaitAsync(cts.Token);
        await botB.Entered.Task.WaitAsync(cts.Token);

        await botA.ChatAsync($"/talk {Npc}");
        await botA.Moved.Task.WaitAsync(cts.Token);
        Assert.Equal(Dungeon, first.MapId);
        Assert.Equal((byte)1, first.Portal); // the dungeon out00
        Assert.Empty(botA.Notices);

        await botB.ChatAsync($"/talk {Npc}");
        await botB.Noticed.Task.WaitAsync(cts.Token);
        Assert.Equal(new[] { "[DEV] busy" }, botB.Notices);
        Assert.Equal(Entrance, second.MapId);
    }

    [Fact]
    public void AdmitRule_EmptyOrOwnParty()
    {
        Assert.True(ChannelHandler.MiniDungeonAdmits(Array.Empty<FieldPlayer>(), null));
        Assert.True(ChannelHandler.MiniDungeonAdmits(Array.Empty<FieldPlayer>(), party: null));
    }
}
