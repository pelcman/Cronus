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
/// A script's <c>player.startQuest</c> / <c>player.completeQuest</c> must take the real quest
/// path (the oracle's forceStartQuest / forceCompleteQuest): the journal record packets, the
/// Act_Success result and the completion effect reach the client, and the character's records
/// move. Before this, the script API only flipped the in-memory dictionaries — the client's
/// journal never heard about it. Skipped when gamedata.db is not next to the repo.
/// </summary>
public class QuestScriptStartCompleteTests
{
    private const int Npc = 9000010;   // any id: no npc-image guard without an INpcNameProvider
    private const int Quest = 2148;    // 噂の真相-豚と一緒に踊りを (a JMS v186 quest with a start script)

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class Bot : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMessage = ServerOps.Get(ServerOpcode.Message);
        private readonly int _opQuestResult = ServerOps.Get(ServerOpcode.UserQuestResult);
        private readonly int _opEffect = ServerOps.Get(ServerOpcode.UserEffectLocal);

        public Bot(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<(int Quest, byte State)> Records { get; } = new();
        public List<(int Quest, int Npc, short Next)> Results { get; } = new();
        public List<byte> Effects { get; } = new();

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
                Entered.TrySetResult(true);
            }
            else if (opcode == _opMessage)
            {
                if (p.ReadByte() == 1) // MS_QuestRecordMessage
                {
                    Records.Add((p.ReadShort(), p.ReadByte()));
                }
            }
            else if (opcode == _opQuestResult)
            {
                if (p.ReadByte() == 8) // Act_Success
                {
                    Results.Add((p.ReadShort(), p.ReadInt(), p.ReadShort()));
                }
            }
            else if (opcode == _opEffect)
            {
                Effects.Add(p.ReadByte());
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

    private static string? RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "gamedata.db")))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    [Fact]
    public async Task ScriptStartAndComplete_ReachTheClientJournal_AndMoveTheRecords()
    {
        string? root = RepoRoot();
        if (root is null)
        {
            return;
        }

        var store = new SqliteWzStore(Path.Combine(root, "gamedata.db"));
        var quests = new WzQuestProvider(store);
        var engine = new NpcScriptEngine(new DictionaryNpcScriptSource(new Dictionary<int, string>
        {
            [Npc] = $"function start() {{ player.startQuest({Quest}); player.completeQuest({Quest}); cm.sendOk(\"done\"); }}",
        }));

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Quester", MapId = 100000000, Level = 200, Job = 112 });
        var maps = new InMemoryMapProvider(new[] { new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() } });
        var fields = new FieldRegistry(maps);
        var bot = new Bot(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, engine, quests: quests);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var client = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, bot);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);
        await bot.Entered.Task.WaitAsync(cts.Token);

        await bot.ChatAsync($"/talk {Npc}");
        DateTime deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline && bot.Records.Count < 2)
        {
            await Task.Delay(50, cts.Token);
        }

        Assert.Equal(new[] { (Quest, ChannelPackets.QuestRecordStarted), (Quest, ChannelPackets.QuestRecordCompleted) }, bot.Records);
        Assert.Contains((Quest, Npc, (short)0), bot.Results.Select(r => (r.Quest, r.Npc, r.Next)));
        Assert.Contains(ChannelPackets.UserEffectQuestComplete, bot.Effects);
        Assert.False(hero.StartedQuests.ContainsKey(Quest));
        Assert.True(hero.CompletedQuests.ContainsKey(Quest));
    }
}
