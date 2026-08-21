using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class HpBarTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void UserHP_HasCidHpMaxHp()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.UserHP(42, 350, 500), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.UserHP), r.ReadHeader());
        Assert.Equal(42, r.ReadInt());   // character id
        Assert.Equal(350, r.ReadInt());  // current hp
        Assert.Equal(500, r.ReadInt());  // max hp
        Assert.Equal(0, r.Remaining);
    }

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

    /// <summary>Alice: forms a party with the partner, then takes a hit once they've joined.</summary>
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
                await Party(session, w => w.WriteByte(1)); // create
            }
            else if (opcode == _opParty)
            {
                int op = p.ReadByte();
                if (op == 8 && !_invited)
                {
                    _invited = true;
                    await Party(session, w => { w.WriteByte(4); w.WriteString(_partnerName); });
                }
                else if (op == 15 && !_hit) // partner joined -> take a hit
                {
                    _hit = true;
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserHit), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteInt(0);        // time
                    w.WriteByte(0);       // nAttackIdx
                    w.WriteByte(0);       // nMagicElemAttr
                    w.WriteInt(_damage);  // nDamage
                    w.WriteInt(0);        // trailing
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

    /// <summary>Bob: joins on invite, records the first damaged HP bar he sees for a party member.</summary>
    private sealed class Watcher : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opParty = ServerOps.Get(ServerOpcode.PartyResult);
        private readonly int _opUserHp = ServerOps.Get(ServerOpcode.UserHP);

        public Watcher(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(int Cid, int Hp, int MaxHp)> DamagedHp { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opParty)
            {
                int op = p.ReadByte();
                if (op == 4) // invite popup -> join
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
                int cid = p.ReadInt();
                int hp = p.ReadInt();
                int maxHp = p.ReadInt();
                if (hp < maxHp) // ignore the full-HP sync sent at join; capture the damaged bar
                {
                    DamagedHp.TrySetResult((cid, hp, maxHp));
                }
            }
        }
    }

    [Fact]
    public async Task PartyMember_TakingDamage_PushesHpBarToPartner()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000, Hp = 500, MaxHp = 500 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000, Hp = 500, MaxHp = 500 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var parties = new PartyRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        // Bob online first so the invite finds him.
        var bobClient = new Watcher(bob.Id);
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

        (int cid, int hp, int maxHp) = await bobClient.DamagedHp.Task.WaitAsync(cts.Token);
        Assert.Equal(alice.Id, cid);   // it's Alice's bar
        Assert.Equal(380, hp);         // 500 - 120
        Assert.Equal(500, maxHp);
    }
}
