using Cronus.Network.Packets;

namespace Cronus.Server.Game;

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

    /// <summary>The caster's level in <see cref="SkillId"/> — the parser leaves it 0 and the
    /// handler fills it from the character's learned skills before mirroring the attack.</summary>
    public int SkillLevel { get; set; }

    /// <summary>Shoot attacks: the USE-inventory slot the bullet (arrow/star) came from; 0 = none.</summary>
    public short BulletSlot { get; init; }

    public required int BuffKey { get; init; }

    public required int AttackActionKey { get; init; }

    public required int AttackSpeed { get; init; }

    /// <summary>Charge time for key-down skills (Big Bang, Hurricane, …); 0 otherwise.</summary>
    public int KeyDown { get; init; }

    public required IReadOnlyList<AttackTarget> Targets { get; init; }
}

/// <summary>
/// Parses the JMS v186 attack requests — <c>CP_UserMeleeAttack</c> / <c>CP_UserMagicAttack</c>
/// (identical layout at v186) and <c>CP_UserShootAttack</c> (melee + three bullet fields) — from
/// <c>ParseCUser_Attack</c> (pre-Big-Bang, JMS &gt;= 186 &amp;&amp; &lt; 187, no skill/keydown
/// specials). The high nibble of the "hit key" is the target count, the low nibble the
/// hits-per-target.
/// </summary>
public static class AttackParser
{
    public static AttackInfo ParseMelee(PacketReader p) => Parse(p, isShoot: false);

    /// <summary>
    /// Parses <c>CP_UserMagicAttack</c>. For JMS v186 the magic layout is byte-identical to melee
    /// (the magic-specific fields only exist GMS &gt;= 95), so this is an alias for
    /// <see cref="ParseMelee"/>.
    /// </summary>
    public static AttackInfo ParseMagic(PacketReader p) => Parse(p, isShoot: false);

    /// <summary>
    /// Parses <c>CP_UserShootAttack</c>: identical to melee plus the three bullet fields (bullet
    /// inventory slot, cash-bullet slot, shoot range) the client writes after dwID, before the
    /// per-target block (ports the <c>CP_UserShootAttack</c> branch of <c>ParseCUser_Attack</c>).
    /// </summary>
    public static AttackInfo ParseShoot(PacketReader p) => Parse(p, isShoot: true);

    private static AttackInfo Parse(PacketReader p, bool isShoot)
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

        // Key-down (charge) skills carry tKeyDown right before the buff key; missing this
        // shifted every later field for Big Bang / Hurricane-class attacks.
        int keyDown = IsKeydownSkill(skillId) ? p.ReadInt() : 0;

        int buffKey = p.ReadByte();
        int attackActionKey = p.ReadShort() & 0xFFFF;
        p.ReadByte();              // nAttackActionType
        int attackSpeed = p.ReadByte();
        p.ReadInt();               // tAttackTime
        p.ReadInt();               // dwID (JMS >= 186)

        short bulletSlot = 0;
        if (isShoot)
        {
            bulletSlot = p.ReadShort(); // ProperBulletPosition (USE-inventory bullet slot)
            p.ReadShort();              // pnCashItemPos (cash-bullet slot)
            p.ReadByte();               // nShootRange0a
        }

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
            BuffKey = buffKey,
            BulletSlot = bulletSlot,
            AttackActionKey = attackActionKey,
            AttackSpeed = attackSpeed,
            KeyDown = keyDown,
            Targets = targets,
        };
    }

    /// <summary>Skills whose REMOTE mirror appends tKeyDown (ports <c>is_keydown_skill_remote</c>:
    /// the Big Bangs and Evan's breaths).</summary>
    public static bool IsKeydownSkillRemote(int skillId)
        => skillId is 2121001 or 2221001 or 2321001 or 22121000 or 22151001;

    /// <summary>Charge-up skills whose REQUEST carries tKeyDown (ports <c>is_keydown_skill</c>).</summary>
    public static bool IsKeydownSkill(int skillId)
        => IsKeydownSkillRemote(skillId)
            || skillId is 3121004 or 3221001      // Storm Arrow / Piercing
                or 5101004 or 5201002 or 5221004  // Screw Punch / Throwing Bomb / Rapid Fire
                or 14111006 or 15101003           // Poison Bomb / Striker Screw Punch
                or 4341002 or 4341003             // Final Cut / Monster Bomb
                or 13111002 or 33121009;          // WB Storm Arrow / WH Wild Shoot

    /// <summary>Meso Explosion — its mirror writes a per-mob hit count (ports <c>is_mesp_explosion</c>).</summary>
    public const int MesoExplosionSkillId = 4211006;
}
