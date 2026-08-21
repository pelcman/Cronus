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

public class MobSkillTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void MobData_ParsesInfoSkillList()
    {
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <imgdir name="2220000.img">
              <imgdir name="info">
                <int name="maxHP" value="2000"/>
                <imgdir name="skill">
                  <imgdir name="0"><int name="skill" value="126"/><int name="level" value="7"/></imgdir>
                  <imgdir name="1"><int name="skill" value="200"/><int name="level" value="78"/></imgdir>
                </imgdir>
              </imgdir>
            </imgdir>
            """;
        string path = Path.Combine(Path.GetTempPath(), $"mobskill-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, xml);
        try
        {
            MobData mob = MobData.FromWz(2220000, WzData.ParseFile(path));
            Assert.Equal(2, mob.Skills.Count);
            Assert.Contains(new MobSkillEntry(126, 7), mob.Skills);
            Assert.Contains(new MobSkillEntry(200, 78), mob.Skills);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FieldMob_Heal_ClampsToMax()
    {
        var mob = new FieldMob { ObjectId = 1, TemplateId = 100100, MaxHp = 100, Hp = 60 };
        Assert.Equal(40, mob.Heal(500));
        Assert.Equal(100, mob.Hp);
        Assert.Equal(0, mob.Heal(10)); // already full
    }

    [Fact]
    public void Field_SpawnMob_AllocatesFreshObjectIds()
    {
        var field = new Field(100000000);
        FieldMob a = field.SpawnMob(100100, null, 10, 20, 3);
        FieldMob b = field.SpawnMob(100100, null, 10, 20, 3);

        Assert.NotEqual(a.ObjectId, b.ObjectId);
        Assert.Equal(-1, a.MobTime); // summons never self-respawn
        Assert.Contains(field.Mobs, m => m.ObjectId == a.ObjectId);
        Assert.Same(a, field.FindMob(a.ObjectId));
    }

    // ---- e2e: hurt mob + heal skill -> ctrl-ack carries the cast and HP comes back ----

    private sealed class Controller : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opController = ServerOps.Get(ServerOpcode.MobChangeController);
        private readonly int _opCtrlAck = ServerOps.Get(ServerOpcode.MobCtrlAck);
        private readonly int _opMobDamaged = ServerOps.Get(ServerOpcode.MobDamaged);

        public Controller(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public TaskCompletionSource<int> Controlling { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(byte Skill, byte Level)> Acked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> Healed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            var w = New(session, ClientOpcode.MigrateIn);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opController)
            {
                p.ReadByte();
                Controlling.TrySetResult(p.ReadInt()); // mob oid
            }
            else if (opcode == _opCtrlAck)
            {
                p.ReadInt();   // oid
                p.ReadShort(); // move id
                p.ReadByte();  // aggro
                p.ReadShort(); // mob mp
                Acked.TrySetResult((p.ReadByte(), p.ReadByte()));
            }
            else if (opcode == _opMobDamaged)
            {
                p.ReadInt();  // oid
                p.ReadByte(); // type
                Healed.TrySetResult(p.ReadInt()); // negative = heal
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask SendMobMoveAsync(int mobOid, bool nextAttack)
        {
            var w = New(Session!, ClientOpcode.MobMove);
            w.WriteInt(mobOid);
            w.WriteShort(1);                     // move id
            w.WriteByte(nextAttack ? (byte)1 : (byte)0);
            w.WriteByte(0);                      // left
            w.WriteInt(0);                       // mob skill
            w.WriteInt(0);                       // JMS >= 186 pair
            w.WriteInt(0);
            w.WriteByte(0);
            w.WriteInt(1);
            w.WriteInt(0x00FFDDCC);
            w.WriteInt(0x00FFDDCC);
            w.WriteInt(0);
            w.WriteBytes(new byte[] { 10, 0, 20, 0, 0, 0, 0, 0 }); // minimal raw path
            await Session!.SendAsync(w.ToArray());
        }

        private static PacketWriter New(MapleSession session, string opcodeName)
            => new(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);
    }

    [Fact]
    public async Task HurtMob_WithHealSkill_CastsAndRestoresHp()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hero", MapId = 100000000 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 2220000, X = 10, Y = 20, Foothold = 1 } },
        };
        var mobs = new InMemoryMobProvider(new[]
        {
            new MobData
            {
                TemplateId = 2220000, MaxHp = 2000,
                Skills = new[] { new MobSkillEntry(114, 1) },
            },
        });
        var skills = new InMemorySkillProvider(mobSkills: new Dictionary<(int, int), MobSkillData>
        {
            [(114, 1)] = new MobSkillData { X = 500, HpThresholdPercent = 100, IntervalMs = 20_000 },
        });
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobs);

        // Wound the mob so the heal has something to restore.
        FieldMob mob = fields.Get(100000000).Mobs[0];
        mob.Hp = 1000;

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Controller(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, skills: skills);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        int mobOid = await client.Controlling.Task.WaitAsync(cts.Token);
        await client.SendMobMoveAsync(mobOid, nextAttack: true);

        (byte skill, byte level) = await client.Acked.Task.WaitAsync(cts.Token);
        Assert.Equal(114, skill);
        Assert.Equal(1, level);

        int healNumber = await client.Healed.Task.WaitAsync(cts.Token);
        Assert.Equal(-500, healNumber);
        Assert.Equal(1500, mob.Hp);
    }

    [Fact]
    public async Task MobSkill_OnCooldown_IsNotRecast()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hero", MapId = 100000000 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Mobs = new[] { new MobSpawn { TemplateId = 2220000, X = 10, Y = 20, Foothold = 1 } },
        };
        var mobs = new InMemoryMobProvider(new[]
        {
            new MobData { TemplateId = 2220000, MaxHp = 2000, Skills = new[] { new MobSkillEntry(114, 1) } },
        });
        var skills = new InMemorySkillProvider(mobSkills: new Dictionary<(int, int), MobSkillData>
        {
            [(114, 1)] = new MobSkillData { X = 500, HpThresholdPercent = 100, IntervalMs = 60_000 },
        });
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }), mobs);
        FieldMob mob = fields.Get(100000000).Mobs[0];
        mob.Hp = 100;
        mob.LastSkillUse[114] = Environment.TickCount64; // just cast

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Controller(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, skills: skills);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        int mobOid = await client.Controlling.Task.WaitAsync(cts.Token);
        await client.SendMobMoveAsync(mobOid, nextAttack: true);

        (byte skill, _) = await client.Acked.Task.WaitAsync(cts.Token);
        Assert.Equal(0, skill); // cooldown swallowed the cast
        Assert.Equal(100, mob.Hp);
    }
}
