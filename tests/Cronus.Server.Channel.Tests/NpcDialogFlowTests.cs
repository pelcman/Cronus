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

/// <summary>
/// Full NPC dialog over the encrypted wire: after entering the game, the client selects an NPC,
/// receives LP_ScriptMessage prompts, answers them, and the JS script advances — all through the
/// real session pipe, exercising ScriptMessage encoding and the answer parser.
/// </summary>
public class NpcDialogFlowTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed record ScriptPrompt(int NpcId, int MessageType, string Text, bool Prev, bool Next);

    private sealed class NpcClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _npcId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opScript = ServerOps.Get(ServerOpcode.ScriptMessage);

        public NpcClient(int characterId, int npcId)
        {
            _characterId = characterId;
            _npcId = npcId;
        }

        private readonly int _opNpcEnter = ServerOps.Get(ServerOpcode.NpcEnterField);

        public MapleSession? Session { get; private set; }
        public System.Collections.Concurrent.BlockingCollection<ScriptPrompt> Prompts { get; } = new();
        public System.Collections.Concurrent.BlockingCollection<(int ObjectId, int TemplateId)> SpawnedNpcs { get; } = new();

        /// <summary>When set, select this object id on spawn instead of the raw template id.</summary>
        public bool SelectSpawnedNpc { get; init; }

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            var w = NewPacket(session, ClientOpcode.MigrateIn);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                if (!SelectSpawnedNpc)
                {
                    await SelectNpcAsync(session, _npcId);
                    if (ChatAfterSelect is not null)
                    {
                        var chat = NewPacket(session, ClientOpcode.UserChat);
                        chat.WriteInt(0);
                        chat.WriteString(ChatAfterSelect);
                        chat.WriteBool(false);
                        await session.SendAsync(chat.ToArray());
                    }
                }
            }
            else if (opcode == ServerOps.Get(ServerOpcode.UserChat))
            {
                ChatReply.TrySetResult();
            }
            else if (opcode == _opNpcEnter)
            {
                int oid = p.ReadInt();
                int template = p.ReadInt();
                SpawnedNpcs.Add((oid, template));
                if (SelectSpawnedNpc)
                {
                    await SelectNpcAsync(session, oid);
                }
            }
            else if (opcode == _opScript)
            {
                p.ReadByte();                 // speaker type
                int npcId = p.ReadInt();
                int messageType = p.ReadByte();
                p.ReadByte();                 // param
                string text = p.ReadString();
                bool prev = false, next = false;
                if (messageType == 0)
                {
                    prev = p.ReadBool();
                    next = p.ReadBool();
                }

                Prompts.Add(new ScriptPrompt(npcId, messageType, text, prev, next));
            }
        }

        /// <summary>Sent right after the select when set — used to prove a click was silent.</summary>
        public string? ChatAfterSelect { get; init; }

        public TaskCompletionSource ChatReply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Clicks the NPC again mid-test.</summary>
        public async ValueTask ReselectAsync(int npcObjectId) => await SelectNpcAsync(Session!, npcObjectId);

        private async ValueTask SelectNpcAsync(MapleSession session, int npcObjectId)
        {
            var w = NewPacket(session, ClientOpcode.UserSelectNpc);
            w.WriteInt(npcObjectId);
            w.WriteShort(0);
            w.WriteShort(0);
            await session.SendAsync(w.ToArray());
        }

        public async ValueTask AnswerAsync(int messageType, int action, int selection = -1, string text = "")
        {
            var w = NewPacket(Session!, ClientOpcode.UserScriptMessageAnswer);
            w.WriteByte(messageType);
            w.WriteByte((byte)action);
            if (action != 0)
            {
                switch (messageType)
                {
                    case 5: w.WriteInt(selection); break;
                    case 3: w.WriteString(text); break;
                }
            }

            await Session!.SendAsync(w.ToArray());
        }

        /// <summary>Sends a deliberately truncated answer (action claims a payload that isn't
        /// there) — the server must not over-read and drop the session.</summary>
        public async ValueTask AnswerTruncatedAsync(int messageType, int action)
        {
            var w = NewPacket(Session!, ClientOpcode.UserScriptMessageAnswer);
            w.WriteByte(messageType);
            w.WriteByte((byte)action); // no payload follows despite action != 0
            await Session!.SendAsync(w.ToArray());
        }

        private static PacketWriter NewPacket(MapleSession session, string opcodeName)
            => new(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);
    }

    [Fact]
    public async Task SelectNpc_RunsScriptedDialogOverWire()
    {
        const int npcId = 9010000;
        const string script = """
            function start() {
                var pick = cm.askMenu("Menu:\r\n#L0#Yes#l\r\n#L1#No#l");
                cm.sendOk(pick == 0 ? "You chose yes." : "You chose no.");
            }
            """;

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Talker", MapId = 100000000 });

        var scripts = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcId] = script }));

        var client = new NpcClient(hero.Id, npcId);
        var handler = new ChannelHandler(
            ClientOps, ServerOps, repo, ServerConfig.Jms186, npcScripts: scripts);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        // First prompt: the menu.
        ScriptPrompt menu = client.Prompts.Take(cts.Token);
        Assert.Equal(npcId, menu.NpcId);
        Assert.Equal(5, menu.MessageType);
        Assert.Contains("Menu", menu.Text);

        await client.AnswerAsync(messageType: 5, action: 1, selection: 0);

        // Second prompt: the ok line reflecting the choice.
        ScriptPrompt ok = client.Prompts.Take(cts.Token);
        Assert.Equal(0, ok.MessageType);
        Assert.Equal("You chose yes.", ok.Text);
        Assert.False(ok.Next);
    }

    [Fact]
    public async Task TruncatedMenuAnswer_DoesNotCrashSession()
    {
        // A menu answer claiming action=1 but with the selection int missing (the crash the
        // debug bot surfaced). The session must survive: the dialog ends and a fresh NPC
        // selection still gets a prompt.
        const int npcId = 9010000;
        const string script = """
            function start() {
                var pick = cm.askMenu("Menu:\r\n#L0#A#l\r\n#L1#B#l");
                cm.sendOk("done " + pick);
            }
            """;

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Truncator", MapId = 100000000 });

        var scripts = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcId] = script }));

        var client = new NpcClient(hero.Id, npcId);
        var handler = new ChannelHandler(
            ClientOps, ServerOps, repo, ServerConfig.Jms186, npcScripts: scripts);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        ScriptPrompt menu = client.Prompts.Take(cts.Token);
        Assert.Equal(5, menu.MessageType);

        // The malformed answer (action claims a selection int that isn't there) would previously
        // over-read and disconnect the session. Now the read is guarded, so the script advances
        // with no selection (-1) and the session survives: its next prompt still arrives.
        await client.AnswerTruncatedAsync(messageType: 5, action: 1);

        ScriptPrompt next = client.Prompts.Take(cts.Token);
        Assert.Equal(0, next.MessageType);       // the sendOk line, not a dropped connection
        Assert.Equal("done -1", next.Text);      // no selection was read
    }

    /// <summary>Enters, selects an NPC, and signals when a second SetField (the warp) arrives.</summary>
    private sealed class WarpingClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _npcId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private int _setFields;

        public WarpingClient(int characterId, int npcId)
        {
            _characterId = characterId;
            _npcId = npcId;
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
                // First SetField = entry; talk to the NPC (which warps us).
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserSelectNpc), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(_npcId);
                w.WriteShort(0);
                w.WriteShort(0);
                await session.SendAsync(w.ToArray());
            }
            else
            {
                Warped.TrySetResult(); // second SetField = the map change
            }
        }
    }

    [Fact]
    public async Task NpcScript_Warp_MovesPlayerToTheMap()
    {
        const int npcId = 9010000;
        const int targetMap = 200000000;
        const string script = "function start() { player.warp(200000000); }";

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Traveler", MapId = 100000000 });

        var maps = new InMemoryMapProvider(new[]
        {
            new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() },
            new MapData { MapId = targetMap, Portals = Array.Empty<PortalData>() },
        });
        var fields = new FieldRegistry(maps);
        var scripts = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcId] = script }));

        var client = new WarpingClient(hero.Id, npcId);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, npcScripts: scripts);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.Warped.Task.WaitAsync(cts.Token);
        Assert.Equal(targetMap, hero.MapId); // the script's player.warp moved the character
    }

    /// <summary>Enters, talks to an NPC, and signals once a fame StatChanged (the script's last op) lands.</summary>
    private sealed class StatWatchingClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _npcId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);

        public StatWatchingClient(int characterId, int npcId)
        {
            _characterId = characterId;
            _npcId = npcId;
        }

        public TaskCompletionSource FameChanged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opSetField)
            {
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserSelectNpc), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(_npcId);
                w.WriteShort(0);
                w.WriteShort(0);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();          // unlock
                int mask = p.ReadInt();
                if ((mask & 0x20000) != 0) // Fame — the script's last mutation
                {
                    FameChanged.TrySetResult();
                }
            }
        }
    }

    [Fact]
    public async Task NpcScript_MutatesJobApSpFame()
    {
        const int npcId = 9010001;
        const string script = """
            function start() {
                player.setJob(200);
                player.gainAp(3);
                player.gainSp(2);
                player.gainFame(5);
            }
            """;

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Novice", MapId = 100000000, Job = 0, Ap = 0, Sp = 0, Fame = 0 });

        var scripts = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcId] = script }));

        var client = new StatWatchingClient(hero.Id, npcId);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, npcScripts: scripts);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.FameChanged.Task.WaitAsync(cts.Token);
        Assert.Equal(200, hero.Job);
        Assert.Equal(3, hero.Ap);
        Assert.Equal(2, hero.Sp);
        Assert.Equal(5, hero.Fame);
    }

    [Fact]
    public async Task NpcSpawnsFromMapData_AndSelectByObjectIdRunsScript()
    {
        const int npcTemplate = 9010000;
        const string script = """
            function start() { cm.sendOk("Hi from " + 9010000 + "!"); }
            """;

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Seeker", MapId = 100000000 });

        // One NPC placed on the map; its runtime object id differs from the template id.
        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Npcs = new[] { new NpcSpawn { TemplateId = npcTemplate, X = 120, Y = -60, Foothold = 7 } },
        };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var scripts = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcTemplate] = script }));

        var client = new NpcClient(hero.Id, npcTemplate) { SelectSpawnedNpc = true };
        var handler = new ChannelHandler(
            ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, npcScripts: scripts);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        // The NPC spawned with a runtime object id distinct from its template id.
        (int oid, int template) = client.SpawnedNpcs.Take(cts.Token);
        Assert.Equal(npcTemplate, template);
        Assert.NotEqual(template, oid);

        // Selecting it by object id resolves to the template's script.
        ScriptPrompt prompt = client.Prompts.Take(cts.Token);
        Assert.Equal(0, prompt.MessageType);
        Assert.Equal("Hi from 9010000!", prompt.Text);
    }

    [Fact]
    public async Task SelectNpc_WithoutScriptShopOrQuests_GetsTheFallbackLine()
    {
        // Script-less, shop-less, quest-less NPCs (the expired-event spawns baked into the v186
        // maps) answer with one flavor line instead of dead silence.
        const int npcId = 9250136; // ビンポス — a real event NPC with nothing behind it
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Poker", MapId = 100000000 });

        var scripts = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string>())); // no scripts at all

        var client = new NpcClient(hero.Id, npcId);
        var handler = new ChannelHandler(
            ClientOps, ServerOps, repo, ServerConfig.Jms186, npcScripts: scripts,
            questNpcs: new InMemoryQuestNpcIndex(Array.Empty<int>()));

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        ScriptPrompt prompt = client.Prompts.Take(cts.Token);
        Assert.Equal(npcId, prompt.NpcId);
        Assert.Equal(0, prompt.MessageType);            // a plain sendOk
        Assert.Contains("話すことはない", prompt.Text);

        // Closing the line ends the conversation; a second click brings it back. The end runs
        // on the fallback thread after our answer, so retry the click until it lands.
        await client.AnswerAsync(messageType: 0, action: 1);
        ScriptPrompt? again = null;
        for (int i = 0; i < 40 && again is null; i++)
        {
            await client.ReselectAsync(npcId);
            client.Prompts.TryTake(out again, 250);
        }

        Assert.NotNull(again);
        Assert.Equal(0, again!.MessageType);
    }

    [Fact]
    public async Task SelectNpc_QuestNpcWithoutScript_StaysSilentForTheClientQuestUi()
    {
        // A quest NPC's click must NOT be answered — the client runs its own quest UI, and a
        // server dialog would swallow it. The follow-up chat command proves the pipeline stayed
        // healthy and that no ScriptMessage snuck in ahead of it.
        const int npcId = 9010010; // カサンドラ — quest NPC in the real data
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Quiet", MapId = 100000000 });

        var scripts = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string>()));

        var client = new NpcClient(hero.Id, npcId) { ChatAfterSelect = "/pos" };
        var handler = new ChannelHandler(
            ClientOps, ServerOps, repo, ServerConfig.Jms186, npcScripts: scripts,
            questNpcs: new InMemoryQuestNpcIndex(new[] { npcId }));

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        // The /pos reply arrives with no ScriptMessage before it (same ordered pipe).
        await client.ChatReply.Task.WaitAsync(cts.Token);
        Assert.Empty(client.Prompts);
    }
}
