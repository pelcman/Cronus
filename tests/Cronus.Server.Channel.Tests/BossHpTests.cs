using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class BossHpTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void MobHpTag_HasIdHpMaxHpAndColors()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var mob = new FieldMob { ObjectId = 2_000_000, TemplateId = 8800000, MaxHp = 1000, Hp = 700, TagColor = 5, TagBgColor = 1 };

        var r = new PacketReader(packets.MobHpTag(mob), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.FieldEffect), r.ReadHeader());
        Assert.Equal(5, r.ReadByte());        // FieldEffect_MobHPTag
        Assert.Equal(2_000_000, r.ReadInt()); // mob object id
        Assert.Equal(700, r.ReadInt());       // current hp
        Assert.Equal(1000, r.ReadInt());      // max hp
        Assert.Equal(5, r.ReadByte());        // tag color
        Assert.Equal(1, r.ReadByte());        // tag bg color
        Assert.Equal(0, r.Remaining);
    }

    // Builds a JMS v186 CP_UserMeleeAttack hitting one mob for one damage line.
    private static byte[] MeleeAttack(MapleSession session, int mobOid, int damage)
    {
        var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserMeleeAttack), session.Config.PacketHeaderSize, session.Config.CodePage);
        w.WriteByte(0);
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteByte(0x11);          // 1 damage, 1 mob
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);              // skill id
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteByte(0);
        w.WriteShort(0);
        w.WriteByte(0);
        w.WriteByte(0);
        w.WriteInt(0);
        w.WriteInt(0);              // dwID
        w.WriteInt(mobOid);
        w.WriteBytes(new byte[4]);
        w.WriteBytes(new byte[8]);
        w.WriteShort(0);
        w.WriteInt(damage);
        w.WriteInt(0);              // mob crc
        return w.ToArray();
    }

    private sealed class BossHitter : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMobEnter = ServerOps.Get(ServerOpcode.MobEnterField);
        private readonly int _opFieldEffect = ServerOps.Get(ServerOpcode.FieldEffect);
        private int _mobOid = -1;
        private bool _setField;
        private bool _attacked;

        public BossHitter(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(int MobId, int Hp, int MaxHp, int TagColor)> Gauge { get; } =
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
            if (opcode == _opSetField)
            {
                _setField = true;
                await MaybeAttack(session);
            }
            else if (opcode == _opMobEnter)
            {
                _mobOid = p.ReadInt();
                await MaybeAttack(session);
            }
            else if (opcode == _opFieldEffect && p.ReadByte() == 5) // MobHPTag
            {
                int mobId = p.ReadInt();
                int hp = p.ReadInt();
                int maxHp = p.ReadInt();
                int tagColor = p.ReadByte();
                Gauge.TrySetResult((mobId, hp, maxHp, tagColor));
            }
        }

        private async ValueTask MaybeAttack(MapleSession session)
        {
            if (_setField && _mobOid >= 0 && !_attacked)
            {
                _attacked = true;
                await session.SendAsync(MeleeAttack(session, _mobOid, 250)); // boss survives (1000 HP)
            }
        }
    }

    [Fact]
    public async Task DamagingBoss_BroadcastsHpGauge()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Slayer", MapId = 100000000 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 8800000, X = 0, Y = 0, MaxHp = 1000 } },
        };
        // A boss: tag colours make it show the HP gauge.
        var mobData = new InMemoryMobProvider(new[]
        {
            new MobData { TemplateId = 8800000, MaxHp = 1000, Exp = 0, TagColor = 5, TagBgColor = 1 },
        });
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobData);

        using var cts = new CancellationTokenSource(Timeout);

        var client = new BossHitter(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (int mobId, int hp, int maxHp, int tagColor) = await client.Gauge.Task.WaitAsync(cts.Token);
        Assert.Equal(fields.Get(100000000).Mobs[0].ObjectId, mobId);
        Assert.Equal(750, hp);     // 1000 - 250
        Assert.Equal(1000, maxHp);
        Assert.Equal(5, tagColor);
    }
}
