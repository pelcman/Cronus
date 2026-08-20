using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Login;
using Xunit;

namespace Cronus.Server.Login.Tests;

/// <summary>
/// Drives the full login-stage flow through the encrypted wire:
/// CheckPassword -> WorldInfoRequest -> SelectWorld, decoding each server reply.
/// </summary>
public class WorldFlowTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed record WorldEntry(int Id, string Name, IReadOnlyList<string> Channels);

    private sealed class FlowClient : PacketHandlerBase
    {
        private readonly int _opCheckPwResult = ServerOps.Get(ServerOpcode.CheckPasswordResult);
        private readonly int _opWorldInfo = ServerOps.Get(ServerOpcode.WorldInformation);
        private readonly int _opSelectWorldResult = ServerOps.Get(ServerOpcode.SelectWorldResult);

        public List<WorldEntry> Worlds { get; } = new();
        public TaskCompletionSource<(int CharCount, int Slots)> SelectWorldResult { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.CheckPassword), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteString("worlduser");
            w.WriteString("pw");
            w.WriteBytes(new byte[16]);
            w.WriteInt(0);
            w.WriteByte(0);
            w.WriteByte(0);
            await session.SendAsync(w.ToArray());
        }

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
        {
            if (opcode == _opCheckPwResult)
            {
                // JMS v186 pushes the world list right after login; the client waits for it
                // rather than requesting it.
                _ = packet.ReadByte();
            }
            else if (opcode == _opWorldInfo)
            {
                int worldId = packet.ReadByte();
                if (worldId == 0xFF)
                {
                    // End of world list -> select world 0, channel 0.
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.SelectWorld), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteByte(0);
                    w.WriteByte(0);
                    await session.SendAsync(w.ToArray());
                    return;
                }

                string name = packet.ReadString();
                packet.ReadByte();          // world state
                packet.ReadString();        // event desc
                packet.ReadShort();         // exp
                packet.ReadShort();         // drop
                int channelCount = packet.ReadByte();
                var channels = new List<string>();
                for (int i = 0; i < channelCount; i++)
                {
                    channels.Add(packet.ReadString());
                    packet.ReadInt();       // user no
                    packet.ReadByte();      // world id
                    packet.ReadByte();      // channel id
                    packet.ReadByte();      // adult flag
                }

                Worlds.Add(new WorldEntry(worldId, name, channels));
            }
            else if (opcode == _opSelectWorldResult)
            {
                packet.ReadByte();          // result
                packet.ReadString();        // JMS marker
                int charCount = packet.ReadByte();
                packet.ReadByte();          // login opt
                packet.ReadByte();
                int slots = packet.ReadInt();
                SelectWorldResult.TrySetResult((charCount, slots));
            }
        }

        private static async ValueTask SendAsync(MapleSession session, string opcodeName)
        {
            var w = new PacketWriter(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);
            await session.SendAsync(w.ToArray());
        }
    }

    [Fact]
    public async Task FullFlow_Login_WorldList_SelectWorld()
    {
        var config = ServerConfig.Jms186;
        var loginService = new LoginService(new InMemoryAccountRepository());
        var client = new FlowClient();

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server,
            new LoginHandler(ClientOps, ServerOps, loginService, config, characterSlots: 3));
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (int CharCount, int Slots) result = await client.SelectWorldResult.Task.WaitAsync(cts.Token);

        // World list arrived and parsed.
        Assert.Single(client.Worlds);
        Assert.Equal("Cronus", client.Worlds[0].Name);
        Assert.Equal(2, client.Worlds[0].Channels.Count);
        Assert.Equal("Cronus-1", client.Worlds[0].Channels[0]);

        // Select-world result: no characters yet, 3 slots.
        Assert.Equal(0, result.CharCount);
        Assert.Equal(3, result.Slots);
    }
}
