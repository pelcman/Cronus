using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>Runs the NPC scripts shipped under scripts/npc/ so a typo or bad API call is caught.</summary>
public class ExampleScriptTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static string ScriptDir => Path.Combine(AppContext.BaseDirectory, "scripts", "npc");

    [Fact]
    public void ShippedScripts_ArePresent()
    {
        Assert.True(File.Exists(Path.Combine(ScriptDir, "1012100.js")));
        Assert.True(File.Exists(Path.Combine(ScriptDir, "9000021.js")));
        Assert.True(File.Exists(Path.Combine(ScriptDir, "9010000.js")));
    }

    /// <summary>Enters, talks to an NPC, and collects the dialog text it says.</summary>
    private sealed class ScriptClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _npcId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opScript = ServerOps.Get(ServerOpcode.ScriptMessage);

        public ScriptClient(int characterId, int npcId)
        {
            _characterId = characterId;
            _npcId = npcId;
        }

        public TaskCompletionSource<string> FirstLine { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            else if (opcode == _opScript)
            {
                p.ReadByte();          // speaker
                p.ReadInt();           // npc id
                p.ReadByte();          // message type
                p.ReadByte();          // param
                FirstLine.TrySetResult(p.ReadString());
            }
        }
    }

    [Fact]
    public async Task JobInstructor_TellsLowLevelBeginnerToComeBack()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Newbie", MapId = 100000000, Level = 5, Job = 0 });

        var scripts = new NpcScriptEngine(new FolderNpcScriptSource(ScriptDir));

        var client = new ScriptClient(hero.Id, 1012100);
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

        string line = await client.FirstLine.Task.WaitAsync(cts.Token);
        Assert.Contains("level 10", line); // the beginner is only level 5
    }
}
