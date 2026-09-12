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
/// <c>/sweep quests</c> draws each quest script's first page on the real client under the NPC the
/// JMS v186 Check data names for that side (a quest script is otherwise unreachable: the client
/// opens it from the journal, so neither /talk nor the NPC sweep ever renders one). The stand-in
/// character is read-only — a swept quest must not start, complete, pay or warp. Skipped when
/// gamedata.db is not next to the repo.
/// </summary>
public class QuestSweepTests
{
    private const int Quest = 2148;      // 噂の真相-豚と一緒に踊りを: JMS Check start npc 1020000
    private const int QuestNpc = 1020000;

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class Bot : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opScript = ServerOps.Get(ServerOpcode.ScriptMessage);

        public Bot(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<(int Npc, int Type, string Text)> Pages { get; } = new();

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
            else if (opcode == _opScript)
            {
                p.ReadByte();                 // speaker type
                int npc = p.ReadInt();
                int type = p.ReadByte();
                p.ReadByte();                 // param
                Pages.Add((npc, type, type is 0 or 2 or 3 or 5 or 13 ? p.ReadString() : string.Empty));
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
    public async Task SweepQuests_DrawsThePageUnderTheQuestsNpc_AndChangesNothing()
    {
        string? root = RepoRoot();
        if (root is null)
        {
            return;
        }

        var store = new SqliteWzStore(Path.Combine(root, "gamedata.db"));
        var quests = new WzQuestProvider(store);
        var engine = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string>()),
            new DictionaryNpcScriptSource(new Dictionary<int, string>
            {
                // Both sides scripted: the sweep must draw each, and neither may take effect.
                [Quest] = "function start() { qm.sendOk('swept start'); player.startQuest(" + Quest + "); player.gainExp(1000); }" +
                          "function end() { qm.sendOk('swept end'); player.completeQuest(" + Quest + "); player.warp(100000000, 0); }",
            }));

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Sweeper", MapId = 100000000, Level = 30, Job = 100, Exp = 0 });
        var maps = new InMemoryMapProvider(new[] { new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() } });
        var fields = new FieldRegistry(maps);
        var bot = new Bot(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, engine, quests: quests);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var client = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, bot);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);
        await bot.Entered.Task.WaitAsync(cts.Token);

        await bot.ChatAsync($"/sweep quests {Quest} 0.05");
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline && bot.Pages.Count < 2)
        {
            await Task.Delay(50, cts.Token);
        }

        await bot.ChatAsync("/sweep stop");

        // Both sides drawn, under the NPC the JMS Check data names for the quest.
        Assert.Equal(2, bot.Pages.Count);
        Assert.All(bot.Pages, page => Assert.Equal(QuestNpc, page.Npc));
        Assert.Equal(new[] { "swept start", "swept end" }, bot.Pages.Select(p => p.Text));

        // Read-only: the stand-in dropped the quest records, the exp and the warp.
        Assert.Empty(hero.StartedQuests);
        Assert.Empty(hero.CompletedQuests);
        Assert.Equal(0, hero.Exp);
        Assert.Equal(100000000, hero.MapId);
    }
}
