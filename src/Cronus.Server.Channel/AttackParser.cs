using Cronus.Network.Packets;

namespace Cronus.Server.Channel;

/// <summary>One attacked monster and the damage dealt to it across the attack's hits.</summary>
public sealed class AttackTarget
{
    public required int MobObjectId { get; init; }

    public required IReadOnlyList<int> Damages { get; init; }

    /// <summary>Total damage to this mob (critical bits masked off).</summary>
    public long TotalDamage => Damages.Sum(d => (long)(d & 0x7FFFFFFF));
}

/// <summary>A parsed melee attack.</summary>
public sealed class AttackInfo
{
    public required int SkillId { get; init; }

    /// <summary>The raw hit key: low nibble = hits/target, high nibble = target count.</summary>
    public required int HitKey { get; init; }

    public required int SkillLevel { get; init; }

    public required int BuffKey { get; init; }

    public required int AttackActionKey { get; init; }

    public required int AttackSpeed { get; init; }

    public required IReadOnlyList<AttackTarget> Targets { get; init; }
}

/// <summary>
/// Parses <c>CP_UserMeleeAttack</c> for JMS v186 (ports <c>ParseCUser_Attack</c>, melee path,
/// pre-Big-Bang, JMS &gt;= 186 &amp;&amp; &lt; 187, no skill/keydown specials). The high nibble of
/// the "hit key" is the target count, the low nibble the hits-per-target.
/// </summary>
public static class AttackParser
{
    public static AttackInfo ParseMelee(PacketReader p)
    {
        p.ReadByte();              // FieldKey
        p.ReadInt();               // DR dr0
        p.ReadInt();               // DR dr1
        int hitKey = p.ReadByte();
        int damagePerMob = hitKey & 0x0F;
        int mobCount = (hitKey >> 4) & 0x0F;
        p.ReadInt();               // DR dr2
        p.ReadInt();               // DR dr3
        int skillId = p.ReadInt();
        p.ReadInt();               // DR get_rand
        p.ReadInt();               // DR crc
        p.ReadInt();               // crc (JMS >= 164)
        int buffKey = p.ReadByte();
        int attackActionKey = p.ReadShort() & 0xFFFF;
        p.ReadByte();              // nAttackActionType
        int attackSpeed = p.ReadByte();
        p.ReadInt();               // tAttackTime
        p.ReadInt();               // dwID (JMS >= 186)

        var targets = new List<AttackTarget>(mobCount);
        for (int i = 0; i < mobCount; i++)
        {
            int mobOid = p.ReadInt();
            p.Skip(4);             // hitAction / foreAction / frameIdx / calcDamageStatIndex
            p.Skip(8);             // 4x mob position/state shorts
            p.ReadShort();         // tDelay
            var damages = new List<int>(damagePerMob);
            for (int j = 0; j < damagePerMob; j++)
            {
                damages.Add(p.ReadInt());
            }

            p.ReadInt();           // mob CRC (JMS >= 164)
            targets.Add(new AttackTarget { MobObjectId = mobOid, Damages = damages });
        }

        return new AttackInfo
        {
            SkillId = skillId,
            HitKey = hitKey,
            SkillLevel = 0, // no skills modeled yet
            BuffKey = buffKey,
            AttackActionKey = attackActionKey,
            AttackSpeed = attackSpeed,
            Targets = targets,
        };
    }
}
