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

public class ComboTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void ComboCast_SetsBit21WithValueOne()
    {
        BuffStat s = Assert.Single(SkillBuff.FromEffect(
            SkillBuff.ComboAttackSkill, new SkillEffect { X = 5, DurationMs = 200000 }));
        Assert.Equal(SkillBuff.ComboCounter, s.Bit);
        Assert.Equal((short)1, s.Value); // 0 orbs = value 1
        Assert.Equal(SkillBuff.ComboAttackSkill, s.Reason);
    }

    /// <summary>Casts Combo, then swings at one mob; expects the orb-count stat update.</summary>
    private sealed class Crusader : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opBuff = ServerOps.Get(ServerOpcode.TemporaryStatSet);
        private bool _cast;
        private bool _swung;

        public Crusader(int characterId) => _characterId = characterId;

        public TaskCompletionSource<short> OrbValue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (opcode == _opSetField && !_cast)
            {
                _cast = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserSkillUseRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);
                w.WriteInt(SkillBuff.ComboAttackSkill);
                w.WriteByte(1);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opBuff)
            {
                if (!_swung)
                {
                    // The activation echo arrived — swing once at a (fake) mob.
                    _swung = true;
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserMeleeAttack), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteByte(0);        // FieldKey
                    w.WriteInt(0); w.WriteInt(0);
                    w.WriteByte(0x11);     // hitKey: 1 target, 1 hit
                    w.WriteInt(0); w.WriteInt(0);
                    w.WriteInt(0);         // no attack skill (plain swing)
                    w.WriteInt(0); w.WriteInt(0); w.WriteInt(0);
                    w.WriteByte(0);        // buff key
                    w.WriteShort(0);       // action key
                    w.WriteByte(0);        // action type
                    w.WriteByte(4);        // attack speed
                    w.WriteInt(0);         // attack time
                    w.WriteInt(0);         // dwID
                    // target block: oid + skips + delay + damage + crc
                    w.WriteInt(424242);
                    w.WriteInt(0);
                    w.WriteLong(0);
                    w.WriteShort(0);
                    w.WriteInt(123);       // one damage line
                    w.WriteInt(0);         // mob crc
                    await session.SendAsync(w.ToArray());
                }
                else
                {
                    // The orb update: 128-bit mask then [value:2][reason:4][duration:4].
                    p.ReadInt(); p.ReadInt(); p.ReadInt(); p.ReadInt();
                    short value = p.ReadShort();
                    int reason = p.ReadInt();
                    if (reason == SkillBuff.ComboAttackSkill)
                    {
                        OrbValue.TrySetResult(value);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task LandingASwing_ChargesAComboOrb()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Crusader", MapId = 100000000, Job = 111, Level = 70,
        });
        hero.Skills[SkillBuff.ComboAttackSkill] = 1;

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var skills = new InMemorySkillProvider(effects: new Dictionary<(int, int), SkillEffect>
        {
            [(SkillBuff.ComboAttackSkill, 1)] = new SkillEffect { X = 3, DurationMs = 200000, MpCon = 10 },
        });

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Crusader(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, skills: skills);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        short value = await client.OrbValue.Task.WaitAsync(cts.Token);
        Assert.Equal(2, value); // one orb charged -> value 2

        FieldPlayer player = fields.Get(100000000).Players.Single();
        Assert.Equal(1, player.ComboOrbs);
    }
}
