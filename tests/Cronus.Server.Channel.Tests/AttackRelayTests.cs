using Cronus.Common;
using Cronus.Network.Packets;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// The attack-mirror layout onlookers parse (ports <c>ResCUserRemote.UserAttack</c>): nSkillID
/// rides after nSLV exactly when nSLV != 0 — omitting it shifted every later byte and crashed
/// the OTHER player's client the first time a skill attack was mirrored in a shared map.
/// </summary>
public class AttackRelayTests
{
    private static ChannelPackets Packets { get; } =
        new(OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties")),
            ServerConfig.Jms186);

    private static AttackInfo Attack(int skillId, int skillLevel, int keyDown = 0) => new()
    {
        SkillId = skillId,
        HitKey = 0x11,
        BuffKey = 0,
        AttackActionKey = 0x8093,
        AttackSpeed = 6,
        KeyDown = keyDown,
        SkillLevel = skillLevel,
        Targets = new[] { new AttackTarget { MobObjectId = 0x1E8484, Damages = new[] { 1234 } } },
    };

    private static PacketReader Walk(byte[] packet)
    {
        var r = new PacketReader(packet, ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        return r;
    }

    [Fact]
    public void SkillAttack_CarriesTheSkillIdAfterTheLevel()
    {
        byte[] built = Packets.UserMagicAttack(characterId: 5, level: 200, Attack(2001004, skillLevel: 30));
        PacketReader r = Walk(built);

        Assert.Equal(5, r.ReadInt());        // dwCharacterID
        Assert.Equal(0x11, r.ReadByte());    // hit key
        Assert.Equal(200, r.ReadByte());     // caster level
        Assert.Equal(30, r.ReadByte());      // nSLV
        Assert.Equal(2001004, r.ReadInt());  // nSkillID — present because nSLV != 0
        r.ReadByte();                        // buff key
        Assert.Equal(0x8093, r.ReadShort() & 0xFFFF); // action key lands aligned
    }

    [Fact]
    public void BasicAttack_OmitsTheSkillId()
    {
        byte[] built = Packets.UserMeleeAttack(characterId: 10, level: 1, Attack(0, skillLevel: 0));
        PacketReader r = Walk(built);

        r.ReadInt(); r.ReadByte(); r.ReadByte();
        Assert.Equal(0, r.ReadByte());       // nSLV == 0
        r.ReadByte();                        // buff key immediately (no skill id)
        Assert.Equal(0x8093, r.ReadShort() & 0xFFFF);
    }

    [Fact]
    public void KeydownMirror_AppendsTheChargeTime()
    {
        // Big Bang (2221001) is a keydown-remote skill: tKeyDown follows the mob block.
        byte[] withCharge = Packets.UserMagicAttack(5, 120, Attack(2221001, 30, keyDown: 777));
        byte[] plain = Packets.UserMagicAttack(5, 120, Attack(2001004, 30, keyDown: 777));

        Assert.Equal(plain.Length + 4, withCharge.Length);
        Assert.Equal(777, BitConverter.ToInt32(withCharge, withCharge.Length - 4));
    }
}
