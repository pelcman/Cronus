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
/// A map's wz <c>info/onUserEnter</c> name runs scripts/map/{name}.js once the client stands on the
/// field (the oracle's MapScriptMethods, e.g. TD_MC_title on entering キノコ城): the screen effect
/// reaches the client, and a script's <c>setQuestData</c> reaches the journal as a started record
/// carrying the data — the client gates "investigate" completions on that string.
/// </summary>
public class MapEnterScriptTests
{
    private const int Map = 106020000;

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class Bot : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMessage = ServerOps.Get(ServerOpcode.Message);
        private readonly int _opFieldEffect = ServerOps.Get(ServerOpcode.FieldEffect);

        public Bot(int characterId) => _characterId = characterId;

        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> ScreenEffects { get; } = new();
        public List<(int Quest, byte State, string Data)> Records { get; } = new();

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
                Entered.TrySetResult(true);
            }
            else if (opcode == _opFieldEffect)
            {
                if (p.ReadByte() == 3) // FieldEffect_Screen
                {
                    ScreenEffects.Add(p.ReadString());
                }
            }
            else if (opcode == _opMessage)
            {
                if (p.ReadByte() == 1) // MS_QuestRecordMessage
                {
                    short quest = p.ReadShort();
                    byte state = p.ReadByte();
                    Records.Add((quest, state, state == ChannelPackets.QuestRecordStarted ? p.ReadString() : string.Empty));
                }
            }

            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task EnteringAScriptedMap_RunsItsScript_EffectAndQuestDataReachTheClient()
    {
        var mapScripts = new PortalScriptEngine(new DictionaryPortalScriptSource(new Dictionary<string, string>
        {
            ["TD_MC_title"] = "function start() { player.showScreenEffect('temaD/enter/mushCatle'); player.setQuestData(2314, '1'); }",
        }));

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Mush", MapId = Map, Level = 35, Job = 112 });
        var maps = new InMemoryMapProvider(new[] { new MapData { MapId = Map, Portals = Array.Empty<PortalData>(), OnUserEnter = "TD_MC_title" } });
        var fields = new FieldRegistry(maps);
        var bot = new Bot(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, mapScripts: mapScripts);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var client = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, bot);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);
        await bot.Entered.Task.WaitAsync(cts.Token);

        DateTime deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline && (bot.ScreenEffects.Count < 1 || bot.Records.Count < 1))
        {
            await Task.Delay(50, cts.Token);
        }

        Assert.Equal(new[] { "temaD/enter/mushCatle" }, bot.ScreenEffects);
        Assert.Contains((2314, ChannelPackets.QuestRecordStarted, "1"), bot.Records);
        Assert.Equal("1", hero.StartedQuests[2314]);
    }

    [Fact]
    public async Task AMapWithoutAScriptForItsName_IsQuietlyANoOp()
    {
        var mapScripts = new PortalScriptEngine(new DictionaryPortalScriptSource(new Dictionary<string, string>()));
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Mush", MapId = Map, Level = 35, Job = 112 });
        var maps = new InMemoryMapProvider(new[] { new MapData { MapId = Map, Portals = Array.Empty<PortalData>(), OnUserEnter = "TD_MC_title" } });
        var fields = new FieldRegistry(maps);
        var bot = new Bot(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, mapScripts: mapScripts);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var client = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, bot);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);
        await bot.Entered.Task.WaitAsync(cts.Token);
        await Task.Delay(300, cts.Token);

        Assert.Empty(bot.ScreenEffects);
        Assert.Empty(bot.Records);
    }
}
