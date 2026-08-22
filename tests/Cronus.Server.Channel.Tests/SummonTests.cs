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

public class SummonTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static FieldSummon MakeSummon() => new()
    {
        ObjectId = 5_000_001,
        OwnerId = 7,
        SkillId = 2311006, // summon dragon
        SkillLevel = 3,
        OwnerLevel = 75,
        X = 100,
        Y = -50,
        Foothold = 12,
        Hp = 0,
        ExpiresAt = DateTime.MaxValue,
    };

    [Fact]
    public void SummonedEnterField_HasExactVanillaLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        var r = new PacketReader(packets.SummonedEnterField(MakeSummon(), animated: false), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.SummonedEnterField), r.ReadHeader());
        Assert.Equal(7, r.ReadInt());          // owner
        Assert.Equal(5_000_001, r.ReadInt());  // summon oid
        Assert.Equal(2311006, r.ReadInt());    // skill
        Assert.Equal(74, r.ReadByte());        // owner level - 1
        Assert.Equal(3, r.ReadByte());         // skill level
        Assert.Equal((short)100, r.ReadShort());
        Assert.Equal((short)-50, r.ReadShort());
        Assert.Equal(4, r.ReadByte());         // move action (alert)
        Assert.Equal((short)12, r.ReadShort()); // foothold
        Assert.Equal(SummonSkills.MoveFly, r.ReadByte()); // dragon flies
        Assert.Equal(SummonSkills.AssistAttack, r.ReadByte());
        Assert.Equal(1, r.ReadByte());         // enter type: create (fresh cast)
        Assert.Equal(0, r.ReadByte());         // avatar-look flag
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void SummonedLeaveAndAttackAndHit_HaveExactLayouts()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        FieldSummon s = MakeSummon();

        var leave = new PacketReader(packets.SummonedLeaveField(s, animated: true), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.SummonedLeaveField), leave.ReadHeader());
        Assert.Equal(7, leave.ReadInt());
        Assert.Equal(5_000_001, leave.ReadInt());
        Assert.Equal(4, leave.ReadByte()); // dead/expired fade
        Assert.Equal(0, leave.Remaining);

        var attack = new PacketReader(
            packets.SummonedAttack(s, animation: 2, new[] { (2_000_000, 1234) }),
            ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.SummonedAttack), attack.ReadHeader());
        Assert.Equal(7, attack.ReadInt());
        Assert.Equal(2311006, attack.ReadInt());
        Assert.Equal(74, attack.ReadByte());
        Assert.Equal(2, attack.ReadByte());        // animation
        Assert.Equal(1, attack.ReadByte());        // hit count
        Assert.Equal(2_000_000, attack.ReadInt()); // mob oid
        Assert.Equal(7, attack.ReadByte());        // filler
        Assert.Equal(1234, attack.ReadInt());      // damage
        Assert.Equal(0, attack.Remaining);

        var hit = new PacketReader(packets.SummonedHit(s, attackAction: 1, damage: 55, mobTemplateIdFrom: 100100), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.SummonedHit), hit.ReadHeader());
        Assert.Equal(7, hit.ReadInt());
        Assert.Equal(2311006, hit.ReadInt());
        Assert.Equal(1, hit.ReadByte());
        Assert.Equal(55, hit.ReadInt());
        Assert.Equal(100100, hit.ReadInt());
        Assert.Equal(0, hit.ReadByte());
        Assert.Equal(0, hit.Remaining);
    }

    [Fact]
    public void SummonSkillTable_MatchesReferenceGroups()
    {
        Assert.True(SummonSkills.IsPuppet(3111002));
        Assert.Equal(SummonSkills.MoveStop, SummonSkills.MoveAbilityOf(3211002));
        Assert.Equal(SummonSkills.AssistNone, SummonSkills.AssistTypeOf(3111002));
        Assert.Equal(SummonSkills.MoveFly, SummonSkills.MoveAbilityOf(3111005));
        Assert.Equal(SummonSkills.MoveWalk, SummonSkills.MoveAbilityOf(2321003));
        Assert.Equal(SummonSkills.AssistHeal, SummonSkills.AssistTypeOf(SummonSkills.Beholder));
        Assert.Equal(SummonSkills.MoveFlyRandom, SummonSkills.MoveAbilityOf(SummonSkills.Gaviota));
        Assert.False(SummonSkills.IsSummon(1301006)); // iron will is a plain buff
    }

    [Fact]
    public void ZakumGate_BodyProtectedWhileAnyArmStands()
    {
        Assert.True(ZakumGate.IsBody(8800000));
        Assert.True(ZakumGate.IsBody(8800002));
        Assert.True(ZakumGate.IsArm(8800003));
        Assert.True(ZakumGate.IsArm(8800010));
        Assert.False(ZakumGate.IsBody(8800003));
        Assert.False(ZakumGate.IsArm(100100));

        var liveArm = new FieldMob { ObjectId = 1, TemplateId = 8800005, MaxHp = 100, Hp = 100 };
        var deadArm = new FieldMob { ObjectId = 2, TemplateId = 8800006, MaxHp = 100, Hp = 0 };
        var body = new FieldMob { ObjectId = 3, TemplateId = 8800000, MaxHp = 100, Hp = 100 };

        Assert.True(ZakumGate.BodyProtected(new[] { liveArm, deadArm, body }));
        Assert.False(ZakumGate.BodyProtected(new[] { deadArm, body })); // all arms down -> body opens
    }

    /// <summary>Provides the summon skill's effect so the cast passes the server checks.</summary>
    private sealed class SummonSkillProvider : ISkillProvider
    {
        public int GetMaxLevel(int skillId) => 30;

        public SkillEffect? GetSkillEffect(int skillId, int level)
            => new() { MpCon = 0, DurationMs = 60_000, X = 500 };

        public MobSkillData? GetMobSkill(int skillId, int level) => null;

        public IReadOnlyList<int> GetSkillIds(int jobId) => Array.Empty<int>();
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

    /// <summary>Migrates in and casts Summon Dragon once the field is up.</summary>
    private sealed class Caster : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private bool _cast;

        public Caster(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_cast)
            {
                _cast = true;
                Ready.TrySetResult();
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserSkillUseRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);        // update time
                w.WriteInt(2311006);  // summon dragon
                w.WriteByte(3);
                await session.SendAsync(w.ToArray());
            }
        }
    }

    /// <summary>Sits in the map and flags the summon spawn broadcast.</summary>
    private sealed class Watcher : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opSummon = ServerOps.Get(ServerOpcode.SummonedEnterField);

        public Watcher(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<(int OwnerId, int SkillId)> Summoned { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opSummon)
            {
                int owner = p.ReadInt();
                p.ReadInt(); // summon oid
                Summoned.TrySetResult((owner, p.ReadInt()));
            }

            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task CastingSummonSkill_SpawnsSummonForTheField()
    {
        var repo = new InMemoryCharacterRepository();
        Character mage = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Priest", MapId = 100000000, Level = 75, Job = 230 });
        mage.Skills[2311006] = 3; // learned
        Character other = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Watcher", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var skills = new SummonSkillProvider();

        using var cts = new CancellationTokenSource(Timeout);

        var watcher = new Watcher(other.Id);
        var watcherHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, skills: skills);
        var w2s = new Pipe();
        var s2w = new Pipe();
        await using var wServer = new MapleSession(w2s.Reader, s2w.Writer, ServerConfig.Jms186, SessionRole.Server, watcherHandler);
        await using var wClient = new MapleSession(s2w.Reader, w2s.Writer, ServerConfig.Jms186, SessionRole.Client, watcher);
        _ = wServer.RunAsync(cts.Token);
        _ = wClient.RunAsync(cts.Token);
        await watcher.Ready.Task.WaitAsync(cts.Token);

        var caster = new Caster(mage.Id);
        var casterHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, skills: skills);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var cServer = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, casterHandler);
        await using var cClient = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, caster);
        _ = cServer.RunAsync(cts.Token);
        _ = cClient.RunAsync(cts.Token);

        (int ownerId, int skillId) = await watcher.Summoned.Task.WaitAsync(cts.Token);
        Assert.Equal(mage.Id, ownerId);
        Assert.Equal(2311006, skillId);

        FieldSummon? standing = fields.Get(100000000).FindSummonBySkill(mage.Id, 2311006);
        Assert.NotNull(standing);
        Assert.Equal(mage.Id, standing!.OwnerId);
    }
}
