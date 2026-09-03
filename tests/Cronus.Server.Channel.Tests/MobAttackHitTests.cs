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
/// The server-side extras of a landed mob attack (ports OnUserHit's MobAttackInfo block): a deadly
/// attack leaves 1 HP / 1 MP and shows hp-1; an MP burn drains MP and leaves HP alone; the mob
/// pays conMP either way.
/// </summary>
public class MobAttackHitTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private const int MobTemplate = 8180000;

    /// <summary>Reports "hit by the mob's attack1 (index 0)" on entry; also records any LP_UserHit
    /// mirror it sees (as the onlooker) and its own LP_StatChanged.</summary>
    private sealed class Hittee : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly Func<int> _mobObjectId;
        private readonly int _damage;
        private readonly bool _reports;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opUserHit = ServerOps.Get("LP_UserHit");
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);
        private bool _sent;

        public Hittee(int characterId, Func<int> mobObjectId, int damage, bool reports)
        {
            _characterId = characterId;
            _mobObjectId = mobObjectId;
            _damage = damage;
            _reports = reports;
        }

        public TaskCompletionSource<int> MirrorDelta { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> StatChanged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opSetField && _reports && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserHit), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);              // time
                w.WriteByte(0);             // nAttackIdx 0 = the mob's attack1
                w.WriteByte(0);             // nMagicElemAttr
                w.WriteInt(_damage);
                w.WriteInt(MobTemplate);    // attacker template
                w.WriteInt(_mobObjectId()); // attacker object id (must match the field's mob)
                w.WriteByte(0);             // nLeft
                w.WriteByte(0);             // nReflect
                w.WriteByte(0);             // unk
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opUserHit)
            {
                p.ReadInt();                // victim
                p.ReadByte();               // attack idx
                p.ReadInt();                // damage
                p.ReadInt();                // template
                p.ReadByte(); p.ReadByte(); p.ReadByte(); // left / reflect / guard
                MirrorDelta.TrySetResult(p.ReadInt());
            }
            else if (opcode == _opStat)
            {
                StatChanged.TrySetResult(true);
            }
        }
    }

    private static (Character Victim, FieldRegistry Fields, InMemoryCharacterRepository Repo, Character Onlooker) World(MobAttackInfo attack)
    {
        var repo = new InMemoryCharacterRepository();
        Character victim = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Victim", MapId = 100000000, Hp = 500, MaxHp = 500, Mp = 300, MaxMp = 300 });
        Character onlooker = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Watch", MapId = 100000000 });
        var mobs = new InMemoryMobProvider(new[]
        {
            new MobData { TemplateId = MobTemplate, MaxHp = 1000, MaxMp = 50, Attacks = new Dictionary<int, MobAttackInfo> { [0] = attack } },
        });
        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = MobTemplate, X = 0, Y = 0, MaxHp = 1000 } },
        };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobs);
        return (victim, fields, repo, onlooker);
    }

    private static async Task<(int MirrorDelta, Character Victim, FieldMob Mob)> RunAsync(MobAttackInfo attack, int damage)
    {
        (Character victim, FieldRegistry fields, InMemoryCharacterRepository repo, Character onlooker) = World(attack);
        FieldMob mob = fields.Get(100000000).Mobs[0];
        using var cts = new CancellationTokenSource(Timeout);

        var watcher = new Hittee(onlooker.Id, () => mob.ObjectId, damage, reports: false);
        var wHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var w2s = new Pipe(); var s2w = new Pipe();
        await using var wServer = new MapleSession(w2s.Reader, s2w.Writer, ServerConfig.Jms186, SessionRole.Server, wHandler);
        await using var wClient = new MapleSession(s2w.Reader, w2s.Writer, ServerConfig.Jms186, SessionRole.Client, watcher);
        _ = wServer.RunAsync(cts.Token);
        _ = wClient.RunAsync(cts.Token);

        // The mirror only reaches players already in the field — wait for the onlooker to land
        // before the victim reports the hit (otherwise this is a race).
        while (!fields.Get(100000000).Players.Any(fp => fp.Character.Id == onlooker.Id))
        {
            await Task.Delay(10, cts.Token);
        }

        var hittee = new Hittee(victim.Id, () => mob.ObjectId, damage, reports: true);
        var hHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var h2s = new Pipe(); var s2h = new Pipe();
        await using var hServer = new MapleSession(h2s.Reader, s2h.Writer, ServerConfig.Jms186, SessionRole.Server, hHandler);
        await using var hClient = new MapleSession(s2h.Reader, h2s.Writer, ServerConfig.Jms186, SessionRole.Client, hittee);
        _ = hServer.RunAsync(cts.Token);
        _ = hClient.RunAsync(cts.Token);

        int delta = await watcher.MirrorDelta.Task.WaitAsync(cts.Token);
        await hittee.StatChanged.Task.WaitAsync(cts.Token);
        return (delta, victim, mob);
    }

    [Fact]
    public async Task DeadlyAttack_LeavesOneHpOneMp_AndShowsHpMinusOne()
    {
        (int delta, Character victim, FieldMob mob) = await RunAsync(
            new MobAttackInfo(DeadlyAttack: true, MpBurn: 0, DiseaseSkill: 0, DiseaseLevel: 0, MpCon: 5), damage: 120);

        Assert.Equal(499, delta);          // hp-1 is what onlookers see, not the reported 120
        Assert.Equal(1, victim.Hp);
        Assert.Equal(1, victim.Mp);
        Assert.Equal(50 - 5, mob.Mp);      // the mob paid conMP
    }

    [Fact]
    public async Task MpBurn_DrainsMp_AndLeavesHpAlone()
    {
        (int delta, Character victim, FieldMob mob) = await RunAsync(
            new MobAttackInfo(DeadlyAttack: false, MpBurn: 400, DiseaseSkill: 0, DiseaseLevel: 0, MpCon: 1), damage: 120);

        Assert.Equal(120, delta);          // the ordinary number is shown
        Assert.Equal(500, victim.Hp);      // HP untouched by an MP burn
        Assert.Equal(0, victim.Mp);        // 300 - 400, floored at 0
        Assert.Equal(49, mob.Mp);
    }
}
