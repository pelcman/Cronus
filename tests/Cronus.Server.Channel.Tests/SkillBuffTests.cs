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

public class SkillBuffTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void FromEffect_GenericStat_UsesSkillReason()
    {
        // Iron Body: pdd buff.
        List<BuffStat> stats = SkillBuff.FromEffect(1001003, new SkillEffect { Pdd = 40, DurationMs = 300000 });
        BuffStat s = Assert.Single(stats);
        Assert.Equal(1, s.Bit);            // CTS_PDD
        Assert.Equal((short)40, s.Value);
        Assert.Equal(1001003, s.Reason);   // positive skill id (items use negative)
        Assert.Equal(300000, s.DurationMs);
    }

    [Fact]
    public void FromEffect_MagicGuard_UsesXForBit9()
    {
        BuffStat s = Assert.Single(SkillBuff.FromEffect(2001002, new SkillEffect { X = 80, DurationMs = 600000 }));
        Assert.Equal(9, s.Bit);
        Assert.Equal((short)80, s.Value);
    }

    [Fact]
    public void FromEffect_Booster_UsesXForBit11()
    {
        BuffStat s = Assert.Single(SkillBuff.FromEffect(1101004, new SkillEffect { X = -2, DurationMs = 200000 }));
        Assert.Equal(11, s.Bit);
        Assert.Equal((short)-2, s.Value);
    }

    [Fact]
    public void FromEffect_HyperBody_SetsMaxHpThenMaxMp()
    {
        List<BuffStat> stats = SkillBuff.FromEffect(1301006, new SkillEffect { X = 60, Y = 60, DurationMs = 300000 });
        Assert.Equal(2, stats.Count);
        Assert.Equal(13, stats[0].Bit); // MaxHP (ascending bit order)
        Assert.Equal(14, stats[1].Bit); // MaxMP
    }

    [Fact]
    public void FromEffect_NoDuration_IsEmpty()
    {
        Assert.Empty(SkillBuff.FromEffect(1001003, new SkillEffect { Pdd = 40, DurationMs = 0 }));
    }

    /// <summary>Casts a buff skill on entry and captures the buff packet's reason field.</summary>
    private sealed class Caster : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _skillId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opBuff = ServerOps.Get(ServerOpcode.TemporaryStatSet);
        private bool _sent;

        public Caster(int characterId, int skillId)
        {
            _characterId = characterId;
            _skillId = skillId;
        }

        public TaskCompletionSource<int> BuffReason { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                w.WriteInt(0);         // update time
                w.WriteInt(_skillId);
                w.WriteByte(1);        // skill level
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opBuff)
            {
                p.ReadInt(); p.ReadInt(); p.ReadInt(); p.ReadInt(); // 128-bit mask
                p.ReadShort();                                      // value
                BuffReason.TrySetResult(p.ReadInt());               // reason
            }
        }
    }

    [Fact]
    public async Task CastingBuffSkill_AppliesBuffWithSkillReason_AndSpendsMp()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Warrior", MapId = 100000000, Mp = 50, MaxMp = 50,
        });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        // Iron Body L1: +2 wdef, 75s, 8 MP.
        var skills = new InMemorySkillProvider(effects: new Dictionary<(int, int), SkillEffect>
        {
            [(1001003, 1)] = new SkillEffect { Pdd = 2, DurationMs = 75000, MpCon = 8 },
        });

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Caster(hero.Id, 1001003);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, skills: skills);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        int reason = await client.BuffReason.Task.WaitAsync(cts.Token);

        Assert.Equal(1001003, reason);   // the positive skill id
        Assert.Equal(42, hero.Mp);       // 50 - 8 MP cost
    }

    /// <summary>Swings a skill-based melee attack (no targets) and waits for the MP StatChanged.</summary>
    private sealed class SkillAttacker : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _skillId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);
        private bool _sent;

        public SkillAttacker(int characterId, int skillId)
        {
            _characterId = characterId;
            _skillId = skillId;
        }

        public TaskCompletionSource MpChanged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

                // A skill melee swing with zero targets (hitKey 0).
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserMeleeAttack), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(0);            // FieldKey
                w.WriteInt(0); w.WriteInt(0);
                w.WriteByte(0);            // hitKey: 0 targets
                w.WriteInt(0); w.WriteInt(0);
                w.WriteInt(_skillId);      // skill id
                w.WriteInt(0); w.WriteInt(0); w.WriteInt(0);
                w.WriteByte(0);            // buff key
                w.WriteShort(0);           // action key
                w.WriteByte(0);            // action type
                w.WriteByte(0);            // attack speed
                w.WriteInt(0);             // attack time
                w.WriteInt(0);             // dwID
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();              // unlock
                if (p.ReadInt() != 0)      // skip the entry's mask-0 updateStat
                {
                    MpChanged.TrySetResult();
                }
            }
        }
    }

    [Fact]
    public async Task SkillAttack_SpendsTheSkillsMpAtTheLearnedLevel()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Slasher", MapId = 100000000, Mp = 50, MaxMp = 50,
        });
        hero.Skills[1001005] = 3; // Slash Blast learned at level 3

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        var skills = new InMemorySkillProvider(effects: new Dictionary<(int, int), SkillEffect>
        {
            [(1001005, 3)] = new SkillEffect { MpCon = 6 }, // attack skill: MP cost, no buff
        });

        using var cts = new CancellationTokenSource(Timeout);

        var client = new SkillAttacker(hero.Id, 1001005);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, skills: skills);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.MpChanged.Task.WaitAsync(cts.Token);

        Assert.Equal(44, hero.Mp); // 50 - 6 (the level-3 cost was used)
    }
}
