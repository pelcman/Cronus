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
/// The automatic version of "try every quest": a wire client asks the server to accept and then
/// complete every quest the client's data defines (2,956 in JMS v186), through the real quest
/// request handler and the real quest data parser. Most are refused (level, job, prerequisites,
/// items) — that is fine; what must never happen is a server exception, which would end the
/// session. A chat command at the end proves the session is still alive after all of them.
/// Skipped when gamedata.db is not next to the repo.
/// </summary>
public class AllQuestsExerciseTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private const byte QuestReqAccept = 1;
    private const byte QuestReqComplete = 2;
    private const byte QuestReqResign = 3;

    private sealed class QuestStormClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly IReadOnlyList<int> _questIds;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opUserChat = ServerOps.Get(ServerOpcode.UserChat);
        private bool _entered;

        public QuestStormClient(int characterId, IReadOnlyList<int> questIds)
        {
            _characterId = characterId;
            _questIds = questIds;
        }

        public MapleSession? Session { get; private set; }
        public int PacketsReceived { get; private set; }
        public TaskCompletionSource<string> Pong { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            PacketsReceived++;
            if (opcode == _opSetField && !_entered)
            {
                _entered = true;
                foreach (int questId in _questIds)
                {
                    await QuestAsync(session, QuestReqAccept, questId);
                    await QuestAsync(session, QuestReqComplete, questId);
                    await QuestAsync(session, QuestReqResign, questId);
                }

                // Processed in order after all of the above: the answer proves the session survived.
                var chat = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                chat.WriteInt(0);
                chat.WriteString("/pos");
                chat.WriteByte(0);
                await session.SendAsync(chat.ToArray());
            }
            else if (opcode == _opUserChat)
            {
                p.ReadInt();
                p.ReadByte();
                string text = p.ReadString();
                if (text.StartsWith("pos:", StringComparison.Ordinal))
                {
                    Pong.TrySetResult(text);
                }
            }
        }

        private static async ValueTask QuestAsync(MapleSession session, byte action, int questId)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserQuestRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteByte(action);
            w.WriteShort((short)questId);
            if (action != QuestReqResign)
            {
                w.WriteInt(9000021);      // an npc id; the handler validates what it needs
            }

            if (action == QuestReqComplete)
            {
                w.WriteInt(-1);           // no reward selection
            }

            await session.SendAsync(w.ToArray());
        }
    }

    private static string? RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "gamedata.db")) && Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    [Fact]
    public async Task AcceptCompleteAndResignEveryQuest_NeverEndsTheSession()
    {
        string? root = RepoRoot();
        if (root is null)
        {
            return; // no client data on this machine
        }

        var store = new SqliteWzStore(Path.Combine(root, "gamedata.db"));
        string? checkXml = store.ReadText("Quest/Check.img.xml");
        Assert.NotNull(checkXml);
        List<int> questIds = WzData.ParseText(checkXml!).Children.Keys
            .Select(k => int.TryParse(k, out int id) ? id : -1)
            .Where(id => id >= 0)
            .OrderBy(id => id)
            .ToList();
        Assert.True(questIds.Count > 1000, $"only {questIds.Count} quests in Check.img");

        var quests = new WzQuestProvider(store);
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Quester", MapId = 100000000, Level = 200, Job = 132, Meso = 999_999_999 });
        var maps = new InMemoryMapProvider(new[] { new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() } });
        var fields = new FieldRegistry(maps);
        var client = new QuestStormClient(hero.Id, questIds);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, quests: quests);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        string pong = await client.Pong.Task.WaitAsync(cts.Token);
        Assert.StartsWith("pos:", pong);
        Assert.True(client.PacketsReceived > questIds.Count / 10, $"only {client.PacketsReceived} packets came back for {questIds.Count} quests × 3 requests");
    }
}
