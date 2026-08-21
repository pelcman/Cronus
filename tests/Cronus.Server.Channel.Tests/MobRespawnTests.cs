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
}
