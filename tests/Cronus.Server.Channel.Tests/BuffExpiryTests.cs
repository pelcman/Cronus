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

public class BuffExpiryTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    // ---- BuffTracker unit tests ----

    [Fact]
    public void Register_ThenTakeExpired_ReturnsOnlyLapsedBuffs()
    {
        var tracker = new BuffTracker();
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        tracker.Register(1, 1001003, 0b10, 10_000, t0);        // lapses at t0+10s
        tracker.Register(1, -2002004, 0b1000_0000, 60_000, t0); // lapses at t0+60s

        List<ActiveBuff> expired = tracker.TakeExpired(1, t0.AddSeconds(15));

        ActiveBuff buff = Assert.Single(expired);
        Assert.Equal(1001003, buff.Reason);
        Assert.Equal(0b10u, buff.Word0Mask);
        Assert.Single(tracker.Snapshot(1)); // the potion is still running
    }

    [Fact]
    public void Register_SameReason_RefreshesInsteadOfStacking()
    {
        var tracker = new BuffTracker();
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        tracker.Register(1, 1001003, 0b10, 10_000, t0);
        tracker.Register(1, 1001003, 0b10, 60_000, t0.AddSeconds(5)); // re-cast

        Assert.Single(tracker.Snapshot(1));
        Assert.Empty(tracker.TakeExpired(1, t0.AddSeconds(15))); // refreshed past the old lapse
    }

    [Fact]
    public void Remove_ReturnsMaskAndDropsBuff()
    {
        var tracker = new BuffTracker();
        tracker.Register(1, -2002004, 0b1000_0000, 60_000);

        Assert.Equal(0b1000_0000u, tracker.Remove(1, -2002004));
        Assert.Empty(tracker.Snapshot(1));
        Assert.Equal(0u, tracker.Remove(1, -2002004)); // already gone
    }

    [Fact]
    public void ZeroDurationOrMask_IsNotTracked()
    {
        var tracker = new BuffTracker();
        tracker.Register(1, 1001003, 0, 60_000);
        tracker.Register(1, 1001003, 0b10, 0);
        Assert.Empty(tracker.Snapshot(1));
    }

    // ---- BuffExpiryService e2e: cast → tick past duration → LP_TemporaryStatReset ----

    /// <summary>Casts Iron Body on entry, then records the buff-set and buff-reset packets.</summary>
    private sealed class Caster : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opBuff = ServerOps.Get(ServerOpcode.TemporaryStatSet);
        private readonly int _opReset = ServerOps.Get(ServerOpcode.TemporaryStatReset);
        private bool _sent;

        public Caster(int characterId) => _characterId = characterId;

        public TaskCompletionSource BuffApplied { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<uint> ResetMask { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserSkillUseRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);
                w.WriteInt(1001003);
                w.WriteByte(1);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opBuff)
            {
                BuffApplied.TrySetResult();
            }
            else if (opcode == _opReset)
            {
                // LP_TemporaryStatReset: 128-bit mask, reverse word order — word[0] is the last dword.
                p.ReadInt(); p.ReadInt(); p.ReadInt();
                ResetMask.TrySetResult((uint)p.ReadInt());
            }
        }
    }

    [Fact]
    public async Task ExpiryTick_SendsTemporaryStatResetWithTheBuffMask()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Warrior", MapId = 100000000, Mp = 50, MaxMp = 50,
        });
        hero.Skills[1001003] = 1;

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var skills = new InMemorySkillProvider(effects: new Dictionary<(int, int), SkillEffect>
        {
            [(1001003, 1)] = new SkillEffect { Pdd = 2, DurationMs = 75000, MpCon = 8 },
        });
        var tracker = new BuffTracker();

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Caster(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, skills: skills, buffs: tracker);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.BuffApplied.Task.WaitAsync(cts.Token);
        ActiveBuff active = Assert.Single(tracker.Snapshot(hero.Id));
        Assert.Equal(1001003, active.Reason);

        // Sweep past the buff's 75s duration: the server pushes the reset itself.
        var expiry = new BuffExpiryService(fields, tracker, new ChannelPackets(ServerOps, ServerConfig.Jms186));
        int sent = await expiry.TickAsync(DateTime.UtcNow.AddSeconds(80));

        Assert.Equal(1, sent);
        uint mask = await client.ResetMask.Task.WaitAsync(cts.Token);
        Assert.Equal(0b10u, mask); // CTS_PDD (Iron Body)
        Assert.Empty(tracker.Snapshot(hero.Id));
    }

    [Fact]
    public async Task ExpiryTick_BeforeDuration_SendsNothing()
    {
        var fields = new FieldRegistry();
        var tracker = new BuffTracker();
        var hero = new Character { Id = 7, Name = "Idle", MapId = 100000000 };
        fields.Get(100000000).Enter(new FieldPlayer(hero, session: null!));
        tracker.Register(hero.Id, 1001003, 0b10, 75_000);

        var expiry = new BuffExpiryService(fields, tracker, new ChannelPackets(ServerOps, ServerConfig.Jms186));
        int sent = await expiry.TickAsync(DateTime.UtcNow.AddSeconds(10));

        Assert.Equal(0, sent);
        Assert.Single(tracker.Snapshot(hero.Id));
    }
}
