using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class AbilityTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private const int CsStr = 0x40; // OpsChangeStat.CS_STR == StatFlag.Str

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    /// <summary>Spends one AP on STR on entry and reads back the STR/AP from the StatChanged.</summary>
    private sealed class Spender : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);
        private bool _sent;

        public Spender(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(int Str, int Ap)> Result { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserAbilityUpRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);       // timestamp
                w.WriteInt(CsStr);   // raise STR
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();        // unlock
                int mask = p.ReadInt();
                if ((mask & CsStr) == 0)
                {
                    return;          // not the ability-up result
                }

                int str = p.ReadShort();       // Str (ascending bit order: 0x40 before 0x4000)
                int ap = p.ReadShort();        // Ap
                Result.TrySetResult((str, ap));
            }
        }
    }

    [Fact]
    public async Task AbilityUp_RaisesStr_AndSpendsAp()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hero", MapId = 100000000, Str = 4, Ap = 3 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Spender(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (int str, int ap) = await client.Result.Task.WaitAsync(cts.Token);
        Assert.Equal(5, str);      // 4 -> 5
        Assert.Equal(2, ap);       // 3 -> 2
        Assert.Equal(5, hero.Str); // persisted on the character
        Assert.Equal(2, hero.Ap);
    }
}
