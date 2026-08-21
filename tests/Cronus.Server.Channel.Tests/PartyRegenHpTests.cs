using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class PartyRegenHpTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static byte[] MigrateIn(MapleSession session, int characterId)
    {
        var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
        w.WriteInt(characterId);
        w.WriteBytes(new byte[16]);
        w.WriteShort(0);
        w.WriteByte(0);
        w.WriteLong(0);
        return w.ToArray();
    }

    /// <summary>Alice: forms a party and takes a hit once the partner joins.</summary>
    private sealed class HurtLeader : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _partnerName;
        private readonly int _damage;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opParty = ServerOps.Get(ServerOpcode.PartyResult);
        private bool _invited;
        private bool _hit;

        public HurtLeader(int characterId, string partnerName, int damage)
        {
            _characterId = characterId;
            _partnerName = partnerName;
            _damage = damage;
        }

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                await Party(session, w => w.WriteByte(1));
            }
            else if (opcode == _opParty)
            {
                int op = p.ReadByte();
                if (op == 8 && !_invited)
                {
                    _invited = true;
                    await Party(session, w => { w.WriteByte(4); w.WriteString(_partnerName); });
                }
                else if (op == 15 && !_hit)
                {
                    _hit = true;
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserHit), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteInt(0);
                    w.WriteByte(0);
                    w.WriteByte(0);
                    w.WriteInt(_damage);
                    w.WriteInt(0);
                    await session.SendAsync(w.ToArray());
                }
            }
        }

        private static async ValueTask Party(MapleSession session, Action<PacketWriter> body)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.PartyRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
            body(w);
            await session.SendAsync(w.ToArray());
        }
    }

    /// <summary>Bob: joins, notes the leader's damaged HP, then the recovered HP from the regen tick.</summary>
    private sealed class RecoveryWatcher : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opParty = ServerOps.Get(ServerOpcode.PartyResult);
        private readonly int _opUserHp = ServerOps.Get(ServerOpcode.UserHP);
        private int _damagedHp = -1;

        public RecoveryWatcher(int characterId) => _characterId = characterId;

        public TaskCompletionSource<int> Damaged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> Recovered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opParty)
            {
                if (p.ReadByte() == 4) // invite popup -> join
                {
                    int partyId = p.ReadInt();
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.PartyRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteByte(3);
                    w.WriteInt(partyId);
                    await session.SendAsync(w.ToArray());
                }
            }
            else if (opcode == _opUserHp)
            {
                p.ReadInt();          // cid
                int hp = p.ReadInt();
                int maxHp = p.ReadInt();
                if (_damagedHp < 0 && hp < maxHp)
                {
                    _damagedHp = hp;
                    Damaged.TrySetResult(hp);
                }
                else if (_damagedHp >= 0 && hp > _damagedHp)
                {
                    Recovered.TrySetResult(hp);
                }
            }
        }
    }

    [Fact]
    public async Task RegenTick_PushesRecoveredHpToPartner()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000, Hp = 500, MaxHp = 500 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000, Hp = 500, MaxHp = 500 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var parties = new PartyRegistry();
        var regen = new PlayerRegenService(fields, new ChannelPackets(ServerOps, ServerConfig.Jms186), parties);

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new RecoveryWatcher(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, channelId: 0, parties: parties);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);

        var aliceClient = new HurtLeader(alice.Id, "Bob", damage: 120);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, channelId: 0, parties: parties);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        // Wait until the party is formed and Alice is hurt (Bob saw her drop to 380).
        int damaged = await bobClient.Damaged.Task.WaitAsync(cts.Token);
        Assert.Equal(380, damaged);

        // Run a regen pass well past the idle threshold; Alice recovers 380 -> 390.
        await regen.TickAsync(Environment.TickCount64 + 10_000);

        int recovered = await bobClient.Recovered.Task.WaitAsync(cts.Token);
        Assert.Equal(390, recovered); // 380 + max(3, 500/50) = 390
    }
}
