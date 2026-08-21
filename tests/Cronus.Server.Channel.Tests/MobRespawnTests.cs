using Cronus.Common;
using Cronus.Data;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class MobRespawnTests
{
    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static (FieldRegistry Fields, MobRespawnService Service) BuildFieldWithMob()
    {
        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 100100, X = 0, Y = 0, MaxHp = 50 } },
        };
        var mobs = new InMemoryMobProvider(new[] { new MobData { TemplateId = 100100, MaxHp = 50, Exp = 10 } });
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobs);
        fields.Get(100000000); // realise the field so the tick sees it
        var service = new MobRespawnService(fields, new ChannelPackets(ServerOps, ServerConfig.Jms186));
        return (fields, service);
    }

    [Fact]
    public async Task Respawns_DeadMob_WhenItsTimeHasArrived()
    {
        (FieldRegistry fields, MobRespawnService service) = BuildFieldWithMob();
        FieldMob mob = Assert.Single(fields.Get(100000000).Mobs);

        mob.Hp = 0;                 // killed
        mob.RespawnAtTick = 1;      // due since tick 1
        Assert.True(mob.IsDead);

        await service.TickAsync(nowTick: long.MaxValue);

        Assert.False(mob.IsDead);
        Assert.Equal(50, mob.Hp);   // back to full HP
        Assert.Equal(0, mob.RespawnAtTick);
        Assert.Equal(-1, mob.ControllerId); // no players → uncontrolled
    }

    [Fact]
    public async Task DoesNotRespawn_BeforeTheDelayElapses()
    {
        (FieldRegistry fields, MobRespawnService service) = BuildFieldWithMob();
        FieldMob mob = Assert.Single(fields.Get(100000000).Mobs);

        mob.Hp = 0;
        mob.RespawnAtTick = 10_000; // due far in the future

        await service.TickAsync(nowTick: 1_000);

        Assert.True(mob.IsDead); // still dead — its time hasn't come
        Assert.Equal(10_000, mob.RespawnAtTick);
    }

    [Fact]
    public async Task LeavesLiveMobsAlone()
    {
        (FieldRegistry fields, MobRespawnService service) = BuildFieldWithMob();
        FieldMob mob = Assert.Single(fields.Get(100000000).Mobs);
        Assert.False(mob.IsDead); // alive, RespawnAtTick == 0

        await service.TickAsync(nowTick: long.MaxValue);

        Assert.False(mob.IsDead);
        Assert.Equal(50, mob.Hp);
    }

    [Fact]
    public void NextRespawnTick_MinusOne_MeansNever()
    {
        Assert.Equal(0, MobRespawnService.NextRespawnTick(-1));
    }

    [Fact]
    public void NextRespawnTick_PositiveMobTime_UsesThatDelayInMs()
    {
        long before = Environment.TickCount64;
        long t = MobRespawnService.NextRespawnTick(5);
        long after = Environment.TickCount64;
        Assert.InRange(t, before + 5000, after + 5000);
    }

    [Fact]
    public void NextRespawnTick_Zero_UsesDefaultDelay()
    {
        long before = Environment.TickCount64;
        long t = MobRespawnService.NextRespawnTick(0);
        long after = Environment.TickCount64;
        Assert.InRange(t, before + MobRespawnService.DelayMs, after + MobRespawnService.DelayMs);
    }

    [Fact]
    public void RemoveExpiredDrops_FadesOldDrops_AndKeepsFreshOnes()
    {
        var field = new Field(100000000);
        var mob = new FieldMob { ObjectId = 1, TemplateId = 100100 };
        FieldDrop drop = field.AddMesoDrop(100, x: 0, y: 0, source: mob);

        // At its drop time it's still fresh.
        Assert.Empty(field.RemoveExpiredDrops(drop.DropAtTick, ttlMs: 1000));

        // Past the TTL it's collected and reported.
        IReadOnlyList<int> expired = field.RemoveExpiredDrops(drop.DropAtTick + 2000, ttlMs: 1000);
        Assert.Equal(new[] { drop.ObjectId }, expired);

        // And it's gone — a pickup now finds nothing.
        Assert.Null(field.RemoveDrop(drop.ObjectId));
    }

    [Fact]
    public async Task DoesNotRespawn_ABossWithMobTimeMinusOne()
    {
        (FieldRegistry fields, MobRespawnService service) = BuildFieldWithMob();
        FieldMob mob = Assert.Single(fields.Get(100000000).Mobs);
        mob.Hp = 0;
        mob.RespawnAtTick = MobRespawnService.NextRespawnTick(-1); // 0 → never
        Assert.Equal(0, mob.RespawnAtTick);

        await service.TickAsync(nowTick: long.MaxValue);

        Assert.True(mob.IsDead); // a -1 mobTime mob stays dead (boss / one-shot)
    }
}
