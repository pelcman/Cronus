using System.IO.Pipelines;
using Cronus.Common;
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
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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

        public MapleSession? Session { get; private set; }
        public System.Collections.Concurrent.BlockingCollection<ScriptPrompt> Prompts { get; } = new();

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
                // Select the NPC once we're in the game.
                var w = NewPacket(session, ClientOpcode.UserSelectNpc);
                w.WriteInt(_npcId);
                w.WriteShort(0);
                w.WriteShort(0);
                await session.SendAsync(w.ToArray());
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
}
