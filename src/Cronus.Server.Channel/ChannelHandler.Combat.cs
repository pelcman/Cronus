// ChannelHandler partial: movement, attacks, mob control, skills, summon spawns, doors.
using System.Security.Cryptography;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

public sealed partial class ChannelHandler
{
    private async ValueTask HandleUserMoveAsync(PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_UserMove: fixed prefix then the raw CMovePath buffer, which is relayed
        // verbatim (ResCUserRemote.UserMove re-emits the parsed bytes unchanged).
        if (packet.Remaining <= MovePrefixLength)
        {
            return;
        }

        packet.Skip(MovePrefixLength);
        byte[] movePath = packet.ReadRemaining();

        UpdatePositionFromMovePath(_player, movePath);
        _player.LastActiveTick = Environment.TickCount64; // moving delays HP/MP regen
        _player.Seated = false;                            // and stands you up

        await _field.BroadcastAsync(
            _packets.UserMove(_player.Character.Id, movePath),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
    }

    private async ValueTask HandleMeleeAttackAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null || _player.Character.Hp <= 0)
        {
            return; // the dead don't swing
        }

        AttackInfo attack = AttackParser.ParseMelee(packet);
        await PrepareSkillAttackAsync(session, attack).ConfigureAwait(false);
        await _field.BroadcastAsync(
            _packets.UserMeleeAttack(_player.Character.Id, _player.Character.Level, attack),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
        await ApplyAttackDamageAsync(session, attack).ConfigureAwait(false);
        await UpdateComboOrbsAsync(session, attack).ConfigureAwait(false);
    }

    // Panic / Coma variants consume the charged combo orbs.
    private static bool ConsumesComboOrbs(int skillId) => skillId is 1111003 or 1111004 or 1111005 or 1111006;

    /// <summary>
    /// Charges (one per landed swing) or consumes (Panic/Coma) Crusader combo orbs, re-sending the
    /// ComboCounter temporary stat with the new count (value = orbs + 1). The reference declares
    /// the CTS bit but never tracks orbs; this uses the already-verified stat-set layout.
    /// </summary>
    private async ValueTask UpdateComboOrbsAsync(MapleSession session, AttackInfo attack)
    {
        Character c = _player!.Character;
        ActiveBuff? combo = _buffs.Find(c.Id, SkillBuff.ComboAttackSkill);
        if (combo is null)
        {
            _player.ComboOrbs = 0;
            return;
        }

        int level = c.Skills.TryGetValue(SkillBuff.ComboAttackSkill, out int lvl) ? lvl : 1;
        int maxOrbs = _skills.GetSkillEffect(SkillBuff.ComboAttackSkill, level)?.X ?? 5;

        int orbs = _player.ComboOrbs;
        if (ConsumesComboOrbs(attack.SkillId))
        {
            orbs = 0;
        }
        else if (attack.Targets.Count > 0)
        {
            orbs = Math.Min(maxOrbs, orbs + 1);
        }

        if (orbs == _player.ComboOrbs)
        {
            return;
        }

        _player.ComboOrbs = orbs;
        int remainingMs = (int)Math.Max(0, (combo.ExpiresAt - DateTime.UtcNow).TotalMilliseconds);
        var stat = new List<BuffStat>
        {
            new(SkillBuff.ComboCounter, (short)(orbs + 1), SkillBuff.ComboAttackSkill, remainingMs),
        };
        await session.SendAsync(_packets.TemporaryStatSet(stat)).ConfigureAwait(false);
    }

    private async ValueTask HandleMagicAttackAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null || _player.Character.Hp <= 0)
        {
            return; // the dead don't cast
        }

        AttackInfo attack = AttackParser.ParseMagic(packet); // v186: same layout as melee
        await PrepareSkillAttackAsync(session, attack).ConfigureAwait(false);
        await _field.BroadcastAsync(
            _packets.UserMagicAttack(_player.Character.Id, _player.Character.Level, attack),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
        await ApplyAttackDamageAsync(session, attack).ConfigureAwait(false);
    }

    private async ValueTask HandleShootAttackAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null || _player.Character.Hp <= 0)
        {
            return; // the dead don't shoot
        }

        AttackInfo attack = AttackParser.ParseShoot(packet);
        await PrepareSkillAttackAsync(session, attack).ConfigureAwait(false);

        // Resolve the bullet (arrow/star/bullet) from the shooter's USE slot so onlookers see the
        // right projectile, and consume one per shot.
        int bulletItemId = 0;
        if (attack.BulletSlot > 0)
        {
            Character c = _player.Character;
            if (Inventory.ItemAt(c, UseTab, attack.BulletSlot) is { } bullet)
            {
                bulletItemId = bullet.ItemId;
                InventoryChange? change = Inventory.RemoveFromSlot(c, UseTab, attack.BulletSlot, 1);
                _characters.Save(c);
                if (change is { } ch)
                {
                    await session.SendAsync(_packets.InventoryOperation(new[] { ch })).ConfigureAwait(false);
                }
            }
        }

        await _field.BroadcastAsync(
            _packets.UserShootAttack(_player.Character.Id, _player.Character.Level, attack, bulletItemId, _player.X, _player.Y),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
        await ApplyAttackDamageAsync(session, attack).ConfigureAwait(false);
    }

    /// <summary>
    /// For a skill-based attack: fills in the caster's learned level (so the field mirror renders
    /// the skill correctly) and deducts the skill's MP cost from wz. Shared by the three attack
    /// handlers; a plain (skill-less) attack is untouched.
    /// </summary>
    private async ValueTask PrepareSkillAttackAsync(MapleSession session, AttackInfo attack)
    {
        if (attack.SkillId <= 0 || _player is null)
        {
            return;
        }

        Character c = _player.Character;
        int level = c.Skills.TryGetValue(attack.SkillId, out int lvl) && lvl > 0 ? lvl : 1;
        attack.SkillLevel = level;

        if (_skills.GetSkillEffect(attack.SkillId, level) is { MpCon: > 0 } effect && c.Mp >= effect.MpCon)
        {
            c.Mp = (short)(c.Mp - effect.MpCon);
            _characters.Save(c);
            await session.SendAsync(_packets.StatChanged(c, StatFlag.Mp)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Applies an attack's per-target damage to the mobs in the field: hurts each live target,
    /// and on death releases control, announces the leave, grants exp, and drops meso. Shared by
    /// the melee / magic / ranged handlers. Damage is currently client-reported (see AGENTS.md).
    /// </summary>
    private async ValueTask ApplyAttackDamageAsync(MapleSession session, AttackInfo attack)
    {
        if (_player is not null)
        {
            _player.LastActiveTick = Environment.TickCount64; // attacking delays HP/MP regen
            _player.Seated = false;                            // and stands you up
        }

        foreach (AttackTarget target in attack.Targets)
        {
            FieldMob? mob = _field!.FindMob(target.MobObjectId);
            if (mob is null || mob.IsDead)
            {
                continue;
            }

            // Server authority: bound the client-reported damage to what a legit pre-BB client
            // can produce (per-line cap) rather than trusting target.TotalDamage verbatim.
            long damage = DamageValidator.ValidatedDamage(target);
            if (ZakumGate.IsBody(mob.TemplateId) && ZakumGate.BodyProtected(_field.Mobs))
            {
                continue; // the body ignores everything while an arm still stands
            }

            mob.Damage(damage > int.MaxValue ? int.MaxValue : (int)damage);

            // Bosses show an HP gauge to the whole field as they're whittled down.
            if (mob.IsBoss)
            {
                await _field!.BroadcastAsync(_packets.MobHpTag(mob)).ConfigureAwait(false);
            }

            if (mob.IsDead)
            {
                mob.ControllerId = -1;
                mob.RespawnAtTick = MobRespawnService.NextRespawnTick(mob.MobTime); // 0 = never (boss)
                await _field.BroadcastAsync(_packets.MobLeaveField(mob.ObjectId)).ConfigureAwait(false);
                await GrantKillExpAsync(mob.Exp).ConfigureAwait(false);
                await UpdateQuestKillsAsync(session, mob.TemplateId).ConfigureAwait(false);
                await DropLootAsync(mob).ConfigureAwait(false);
                await SpawnRevivesAsync(mob).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Spawns a dead boss's next phase in place (ports <c>MapleMonster.spawnRevives</c> — the wz
    /// <c>info/revive</c> list): Zakum's body chain, Papulatus' second clock, and the like.
    /// </summary>
    private async ValueTask SpawnRevivesAsync(FieldMob dead)
    {
        if (_field is null || _fields.MobProvider?.GetMob(dead.TemplateId) is not { } stats
            || stats.Revives.Count == 0)
        {
            return;
        }

        foreach (int reviveId in stats.Revives)
        {
            MobData? reviveStats = _fields.MobProvider?.GetMob(reviveId);
            FieldMob phase = _field.SpawnMob(reviveId, reviveStats, dead.X, dead.Y, dead.Foothold);
            await _field.BroadcastAsync(_packets.MobEnterField(phase)).ConfigureAwait(false);
            if (_player is not null)
            {
                phase.ControllerId = _player.Character.Id;
                await TrySendAsync(_player, _packets.MobChangeController(phase)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Rolls a killed mob's drop table and spawns the loot on the field (ports
    /// <c>TacosReward.dropFromDatabase</c>): each entry drops on a <c>rand(0..999) &lt; chance</c> test
    /// (bosses drop unconditionally), meso rows become meso piles and item rows become item stacks,
    /// fanned out horizontally. A mob with no drop table falls back to a small meso pile so a kill
    /// still rewards. Equip drops are deferred until the equip item body is client-verified.
    /// </summary>
    private async ValueTask DropLootAsync(FieldMob mob)
    {
        if (_field is null)
        {
            return;
        }

        IReadOnlyList<DropEntry> entries = _dropTable.GetDrops(mob.TemplateId);
        if (entries.Count == 0)
        {
            await DropPlaceholderMesoAsync(mob).ConfigureAwait(false);
            return;
        }

        int dropped = 0;
        foreach (DropEntry entry in entries)
        {
            // Monster cards stop dropping once the killer's registered count reaches the
            // GameConstants threshold (this server: 1 — one pickup ends the farm; reference: 5).
            if (entry.ItemId / 10_000 == 238
                && _player is not null
                && _player.Character.MonsterCards.TryGetValue(entry.ItemId, out int cardCount)
                && cardCount >= GameConstants.MonsterCardStopDropCount)
            {
                continue;
            }

            // Quest-locked drops only fall for a killer who is on that quest (the reference gates
            // them by quest status; per-viewer visibility is simplified to the killer's status).
            if (entry.QuestId > 0 && _player?.Character.StartedQuests.ContainsKey(entry.QuestId) != true)
            {
                continue;
            }

            if (!DropRoller.ShouldDrop(entry, Random.Shared.Next(1000), forced: mob.IsBoss, rate: _rates.Drop))
            {
                continue;
            }

            short x = (short)(mob.X + DropRoller.ScatterX(dropped));
            if (entry.ItemId == 0)
            {
                int meso = (int)(DropRoller.MesoAmount(entry, Random.Shared.Next) * _rates.Meso);
                if (meso <= 0)
                {
                    continue;
                }

                FieldDrop drop = _field.AddMesoDrop(meso, x, mob.Y, mob);
                await _field.BroadcastAsync(_packets.DropEnterFieldMeso(drop)).ConfigureAwait(false);
            }
            else
            {
                int qty = DropRoller.ItemQuantity(entry, Random.Shared.Next);
                FieldDrop drop = _field.AddItemDrop(entry.ItemId, (short)Math.Clamp(qty, 1, short.MaxValue), x, mob.Y, mob);
                await _field.BroadcastAsync(_packets.DropEnterFieldItem(drop)).ConfigureAwait(false);
            }

            dropped++;
        }
    }

    /// <summary>Drops a small meso pile for a mob with no drop table (so kills still reward).</summary>
    private async ValueTask DropPlaceholderMesoAsync(FieldMob mob)
    {
        if (_field is null)
        {
            return;
        }

        int meso = Math.Max(1, (int)(mob.MaxHp / 5 * _rates.Meso)); // placeholder formula
        FieldDrop drop = _field.AddMesoDrop(meso, mob.X, mob.Y, mob);
        await _field.BroadcastAsync(_packets.DropEnterFieldMeso(drop)).ConfigureAwait(false);
    }

    /// <summary>
    /// Byte length of the JMS v186 CP_MobMove fields between the skill int and the CMovePath:
    /// int ×2 (JMS &gt;= 186), byte, int, 0xFFDDCC ×2, int.
    /// </summary>
    private const int MobMoveMidLength = 4 + 4 + 1 + 4 + 4 + 4 + 4;

    /// <summary>
    /// Handles <c>CP_NpcMove</c> — the controlling client drives NPC idle animation / random chat
    /// balloons / movement, and the server relays it to the whole field (ports
    /// <c>ReqCNpcPool.OnPacket</c> CP_NpcMove: echo chatIdx, one-time action, and the raw
    /// CMovePath). Without this echo every NPC stands frozen with no idle animation.
    /// </summary>
    private async ValueTask HandleNpcMoveAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_NpcMove: [npcOid:4][chatIdx:1][oneTimeAction:1][movePath raw, optional]
        int npcOid = packet.ReadInt();
        if (packet.Remaining < 2 || _field.FindNpc(npcOid) is null)
        {
            return;
        }

        byte chatIdx = packet.ReadByte();
        byte oneTimeAction = packet.ReadByte();
        byte[] movePath = packet.ReadRemaining();

        // The reference broadcasts to everyone, the mover included.
        await _field.BroadcastAsync(_packets.NpcMove(npcOid, chatIdx, oneTimeAction, movePath)).ConfigureAwait(false);
    }

    private async ValueTask HandleMobMoveAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_MobMove:
        //   [mobOid:4][moveId:2][nextAttack:1][left:1][mobSkill:4][mid fields][movePath raw]
        int mobOid = packet.ReadInt();
        short moveId = packet.ReadShort();
        bool nextAttackPossible = packet.ReadBool();
        byte left = packet.ReadByte();
        int mobSkill = packet.ReadInt();

        if (packet.Remaining <= MobMoveMidLength)
        {
            return;
        }

        packet.Skip(MobMoveMidLength);
        byte[] movePath = packet.ReadRemaining();

        FieldMob? mob = _field.FindMob(mobOid);
        if (mob is null || mob.IsDead)
        {
            return;
        }

        // Only the assigned controller may steer the mob; adopt it if it has none.
        int characterId = _player.Character.Id;
        if (mob.ControllerId is -1)
        {
            mob.ControllerId = characterId;
        }
        else if (mob.ControllerId != characterId)
        {
            return;
        }

        // Track the path origin as the mob's position (same convention as player movement).
        if (movePath.Length >= 4)
        {
            mob.X = (short)(movePath[0] | (movePath[1] << 8));
            mob.Y = (short)(movePath[2] | (movePath[3] << 8));
        }

        // The controller signalled the mob may act: the server picks a castable skill (ports
        // MobUsesSkill) and answers it in the ack so the client animates the cast.
        (byte ackSkill, byte ackLevel) = nextAttackPossible
            ? await TryCastMobSkillAsync(mob).ConfigureAwait(false)
            : ((byte)0, (byte)0);

        await session.SendAsync(_packets.MobCtrlAck(mob, moveId, aggro: false, ackSkill, ackLevel)).ConfigureAwait(false);
        await _field.BroadcastAsync(
            _packets.MobMove(mob.ObjectId, nextAttackPossible, left, mobSkill, movePath),
            exceptCharacterId: characterId).ConfigureAwait(false);
    }

    /// <summary>
    /// Picks and applies one of the mob's wz skills (ports <c>MobUsesSkill</c> + the working scope
    /// of <c>MobSkill.applyEffect</c>): a random known skill, gated by its cooldown and the mob's
    /// HP%% threshold. Self-heal (114) restores HP with a green number; summon (200) spawns the
    /// skill's mobs at the caster (capped by the wz limit). Returns the skill to ack, or (0,0).
    /// </summary>
    private async ValueTask<(byte SkillId, byte Level)> TryCastMobSkillAsync(FieldMob mob)
    {
        if (_field is null || _fields.MobProvider?.GetMob(mob.TemplateId) is not { } stats || stats.Skills.Count == 0)
        {
            return (0, 0);
        }

        MobSkillEntry pick = stats.Skills[Random.Shared.Next(stats.Skills.Count)];
        if (_skills.GetMobSkill(pick.SkillId, pick.Level) is not { } mobSkill)
        {
            return (0, 0);
        }

        long now = Environment.TickCount64;
        if (mob.LastSkillUse.TryGetValue(pick.SkillId, out long last) && now - last <= mobSkill.IntervalMs)
        {
            return (0, 0); // still cooling down
        }

        if (mob.MaxHp > 0 && mob.Hp * 100L / mob.MaxHp > mobSkill.HpThresholdPercent)
        {
            return (0, 0); // not hurt enough to cast
        }

        mob.LastSkillUse[pick.SkillId] = now;
        mob.Mp = (short)Math.Max(0, mob.Mp - mobSkill.MpCon);

        switch (pick.SkillId)
        {
            case 114: // self-heal: green number + HP back
            {
                int healed = mob.Heal(mobSkill.X);
                if (healed > 0)
                {
                    await _field.BroadcastAsync(_packets.MobDamaged(mob, -healed)).ConfigureAwait(false);
                }

                break;
            }

            case 200: // summon minions at the caster, up to the wz field cap
            {
                int alive = _field.Mobs.Count(m => !m.IsDead);
                foreach (int summonId in mobSkill.Summons)
                {
                    if (mobSkill.Limit > 0 && alive >= mobSkill.Limit)
                    {
                        break;
                    }

                    MobData? summonStats = _fields.MobProvider?.GetMob(summonId);
                    FieldMob summon = _field.SpawnMob(summonId, summonStats, mob.X, mob.Y, mob.Foothold);
                    alive++;
                    await _field.BroadcastAsync(_packets.MobEnterField(summon)).ConfigureAwait(false);

                    // Delegate the new mob's AI to this controller's client.
                    summon.ControllerId = _player!.Character.Id;
                    await TrySendAsync(_player, _packets.MobChangeController(summon)).ConfigureAwait(false);
                }

                break;
            }
        }

        return ((byte)pick.SkillId, (byte)pick.Level);
    }

    /// <summary>
    /// Handles <c>CP_UserHit</c> — the client reports the damage its player took from a mob.
    /// Applies the HP loss and pushes <c>LP_StatChanged</c>. HP is floored at 1 for now (death /
    /// revive is a follow-up). Damage is client-reported, the MapleStory norm (see AGENTS.md).
    /// </summary>
    private async ValueTask HandleUserHitAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        // JMS v186 CP_UserHit: [time:4][nAttackIdx:1][nMagicElemAttr:1][nDamage:4], then a body
        // by attack index (ports OnUserHit; the v186 index table is Mob_Magic=0, Mob_Physical=-1,
        // Obstacle=-2, Stat=-3, Counter=-1000).
        packet.ReadInt();                          // time
        sbyte attackIdx = (sbyte)packet.ReadByte();
        packet.ReadByte();                         // nMagicElemAttr
        int rawDamage = packet.ReadInt();

        int mobTemplateId = 0;
        byte left = 0;
        switch (attackIdx)
        {
            case -2 or -3:                         // obstacle / stat damage — no attacker block
                if (packet.Remaining >= 2)
                {
                    packet.ReadShort();            // dwObstacleData
                }

                break;

            case 0 or -1:                          // mob magic / physical attack
                if (packet.Remaining >= 11)
                {
                    mobTemplateId = packet.ReadInt();
                    packet.ReadInt();              // mob object id
                    left = packet.ReadByte();
                    packet.ReadByte();             // nReflect (power guard not modelled)
                    packet.ReadByte();             // unk
                }

                break;

            default:
                return;                            // -1000 counter / not-coded forms
        }

        if (rawDamage < 0)
        {
            return; // the fake-skill (-1) form needs a fake-skill registry we don't keep
        }

        int damage = DamageValidator.ClampLine(rawDamage);
        Character c = _player.Character;

        // Everyone else sees the hit — the damage number (or MISS at 0) and the flinch (ports
        // the unconditional broadcastMessage in every OnUserHit branch).
        if (_field is not null)
        {
            await _field.BroadcastAsync(
                _packets.UserHit(c.Id, attackIdx, damage, mobTemplateId, left, delta: damage),
                exceptCharacterId: c.Id).ConfigureAwait(false);
        }

        if (damage <= 0)
        {
            return; // a miss — mirrored above, nothing to apply
        }

        _player.LastActiveTick = Environment.TickCount64; // taking a hit counts as activity
        c.Hp = (short)Math.Max(0, c.Hp - damage);          // 0 HP = dead (client shows the tombstone)

        StatFlag changed = StatFlag.Hp;
        if (c.Hp == 0)
        {
            changed |= CharacterProgression.ApplyDeathPenalty(c, _maps.GetMap(c.MapId)?.IsTown == true); // dying costs some exp
        }

        await session.SendAsync(_packets.StatChanged(c, changed)).ConfigureAwait(false);
        await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false); // party sees the health drop
    }

    /// <summary>
    /// Handles <c>CP_UserAbilityUpRequest</c> — spends one ability point on a base stat (ports
    /// <c>ReqCUser.OnUserAbilityUpRequest</c>). The flag is a <c>CS_*</c> bit that maps 1:1 onto
    /// <see cref="StatFlag"/>. Rejected requests (no AP / capped) send nothing, matching the client
    /// which only updates from the resulting <c>LP_StatChanged</c>.
    /// </summary>
    private async ValueTask HandleAbilityUpAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        packet.ReadInt();                          // timestamp
        var stat = (StatFlag)packet.ReadInt();     // CS_* flag == StatFlag bit

        StatFlag changed = CharacterProgression.SpendAbilityPoint(_player.Character, stat, EffectResolverFor(_player.Character));
        if (changed == 0)
        {
            return; // no AP, capped stat, or a non-assignable flag
        }

        _characters.Save(_player.Character);
        await session.SendAsync(_packets.StatChanged(_player.Character, changed)).ConfigureAwait(false);
    }

    /// <summary>Upper bound on mass-up allocations (the client sends 2; guards a malformed count).</summary>
    private const int MaxAbilityAllocations = 8;

    /// <summary>
    /// Handles <c>CP_UserAbilityMassUpRequest</c> — the auto-assign button that spends all AP across
    /// several base stats at once (ports <c>OnUserAbilityMassUpRequest</c>). Reads the
    /// <c>[stat:4][points:4]</c> pairs and applies them via
    /// <see cref="CharacterProgression.SpendAllAbilityPoints"/>; an invalid batch is ignored.
    /// </summary>
    private async ValueTask HandleAbilityMassUpAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        packet.ReadInt();                 // timestamp
        int count = packet.ReadInt();
        if (count < 1 || count > MaxAbilityAllocations)
        {
            return;
        }

        var allocations = new List<(StatFlag, int)>(count);
        for (int i = 0; i < count; i++)
        {
            var stat = (StatFlag)packet.ReadInt();
            int points = packet.ReadInt();
            allocations.Add((stat, points));
        }

        StatFlag changed = CharacterProgression.SpendAllAbilityPoints(_player.Character, allocations);
        if (changed == 0)
        {
            return;
        }

        _characters.Save(_player.Character);
        await session.SendAsync(_packets.StatChanged(_player.Character, changed)).ConfigureAwait(false);
    }

    private async ValueTask HandleSkillUpAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        // JMS v186 CP_UserSkillUpRequest: [timeStamp:4][skillId:4]
        packet.ReadInt();
        int skillId = packet.ReadInt();

        Character c = _player.Character;
        if (c.Sp <= 0)
        {
            return; // no SP to spend
        }

        c.Skills.TryGetValue(skillId, out int current);

        // Cap at the skill's wz max level (when known) so SP can't over-level a skill.
        int maxLevel = _skills.GetMaxLevel(skillId);
        if (maxLevel > 0 && current >= maxLevel)
        {
            return; // already maxed
        }

        int level = current + 1;
        c.Skills[skillId] = level;
        c.Sp = (short)Math.Max(0, c.Sp - 1);
        _characters.Save(c);

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Sp)).ConfigureAwait(false);
        await session.SendAsync(_packets.ChangeSkillRecordResult(skillId, level)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserSkillUseRequest</c> — casting a self-buff skill (ports
    /// <c>ReqCUser.OnUserSkillUseRequest</c> + <c>TacosBuff.update</c>): acks with
    /// <c>LP_SkillUseResult</c>, deducts the skill's MP cost, and applies its temporary stat buff via
    /// <c>LP_TemporaryStatSet</c> (reason = the positive skill id, duration from wz). Attack skills go
    /// through the attack handlers, not here. The client only offers skills the player owns, so skill
    /// ownership isn't re-validated server-side yet.
    /// </summary>
    private async ValueTask HandleSkillUseAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        // JMS v186 CP_UserSkillUseRequest (self-buff): [updateTime:4][skillId:4][skillLevel:1]
        packet.ReadInt();
        int skillId = packet.ReadInt();
        packet.ReadByte(); // client-claimed level — the server uses the learned level instead

        // The reference acks every cast unconditionally.
        await session.SendAsync(_packets.SkillUseResult()).ConfigureAwait(false);

        Character c = _player.Character;
        int level = c.Skills.TryGetValue(skillId, out int learned) ? learned : 0;
        if (level <= 0)
        {
            // Not learned — refuse the cast but release the client (the oracle's uncoded-skill
            // path acks and then sends sendStatChanged(true)).
            await session.SendAsync(_packets.StatChanged(c, 0)).ConfigureAwait(false);
            return;
        }

        SkillEffect? effect = _skills.GetSkillEffect(skillId, level);
        if (effect is null)
        {
            await session.SendAsync(_packets.StatChanged(c, 0)).ConfigureAwait(false); // unknown skill
            return;
        }
        if (effect.MpCon > 0 && c.Mp >= effect.MpCon)
        {
            c.Mp = (short)(c.Mp - effect.MpCon);
            _characters.Save(c);
            await session.SendAsync(_packets.StatChanged(c, StatFlag.Mp)).ConfigureAwait(false);
        }

        // A cooldown skill starts the client's cooldown timer (the client blocks recasts).
        if (effect.CooltimeSec > 0)
        {
            await session.SendAsync(_packets.SkillCooltimeSet(skillId, effect.CooltimeSec)).ConfigureAwait(false);
        }

        // A summon skill also spawns its summon in the field.
        if (_field is not null && SummonSkills.IsSummon(skillId))
        {
            await SpawnSummonAsync(c, skillId, level, effect).ConfigureAwait(false);
        }

        // Mystic Door opens a portal pair: here, and at a door spot in the return town.
        if (_field is not null && skillId == MysticDoor.SkillMysticDoor)
        {
            await SpawnDoorAsync(session, c, effect).ConfigureAwait(false);
        }

        List<BuffStat> buffs = SkillBuff.FromEffect(skillId, effect);
        if (buffs.Count == 0)
        {
            return;
        }

        byte[] buffPacket = _packets.TemporaryStatSet(buffs);
        ulong mask = BuffEffect.Mask64(buffs);
        _buffs.Register(c.Id, skillId, mask, effect.DurationMs); // state before the packet
        if (skillId == SkillBuff.ComboAttackSkill)
        {
            _player.ComboOrbs = 0; // a fresh combo starts uncharged
        }

        await session.SendAsync(buffPacket).ConfigureAwait(false);

        // A party buff (Haste, Rage, Hyper Body, … — marked by the wz affect box) also lands on
        // party members in the same map (ports the isPartyBuff apply; range box simplified to map).
        if (effect.HasPartyArea && _parties.GetForCharacter(c.Id) is { } party)
        {
            foreach (FieldPlayer member in party.Members)
            {
                if (member.Character.Id != c.Id && member.Character.MapId == c.MapId)
                {
                    _buffs.Register(member.Character.Id, skillId, mask, effect.DurationMs);
                    await TrySendAsync(member, buffPacket).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Handles <c>CP_UserSkillCancelRequest</c> — the player ends a skill buff early (ports
    /// <c>OnUserSkillCancelRequest</c>): clears that skill's temporary-stat mask with
    /// <c>LP_TemporaryStatReset</c> (recomputed from the skill's wz effect at the player's level).
    /// </summary>
    private async ValueTask HandleSkillCancelAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        int skillId = packet.ReadInt();
        Character c = _player.Character;
        int level = c.Skills.TryGetValue(skillId, out int lvl) ? lvl : 1;
        if (_skills.GetSkillEffect(skillId, level) is not { } effect)
        {
            return;
        }

        ulong mask = BuffEffect.Mask64(SkillBuff.FromEffect(skillId, effect));
        if (mask != 0)
        {
            _buffs.Remove(c.Id, skillId);
            await session.SendAsync(_packets.TemporaryStatReset(mask)).ConfigureAwait(false);

            // Onlookers get the remote cancel so the buff's aura visuals stop for them too
            // (ports OnUserSkillCancelRequest's broadcastMessage).
            if (_field is not null)
            {
                await _field.BroadcastAsync(_packets.UserSkillCancel(c.Id, skillId), exceptCharacterId: c.Id).ConfigureAwait(false);
            }
        }

        // Cancelling a summon skill dismisses the summon too.
        if (_field is not null && SummonSkills.IsSummon(skillId)
            && _field.FindSummonBySkill(c.Id, skillId) is { } summon)
        {
            _field.RemoveSummon(summon.ObjectId);
            await _field.BroadcastAsync(_packets.SummonedLeaveField(summon, animated: false)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Spawns the summon a cast produces and shows it to the field (ports the
    /// <c>MapleStatEffect.applyTo</c> summon branch). Recasting replaces the standing one.
    /// </summary>
    private async ValueTask SpawnSummonAsync(Character c, int skillId, int level, SkillEffect effect)
    {
        Field field = _field!;
        FieldPlayer player = _player!;

        if (field.FindSummonBySkill(c.Id, skillId) is { } old)
        {
            field.RemoveSummon(old.ObjectId);
            await field.BroadcastAsync(_packets.SummonedLeaveField(old, animated: false)).ConfigureAwait(false);
        }

        int durationMs = effect.DurationMs > 0 ? effect.DurationMs : 60_000;
        int hp = effect.X + (skillId == SummonSkills.Beholder ? 1 : 0); // puppet HP comes from x
        FieldSummon summon = field.AddSummon(
            c.Id, skillId, level, c.Level, player.X, player.Y, foothold: 0, hp,
            DateTime.UtcNow.AddMilliseconds(durationMs));
        await field.BroadcastAsync(_packets.SummonedEnterField(summon, animated: false)).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens a Mystic Door pair (ports the <c>MapleStatEffect</c> magic-door branch): the field
    /// side at the caster's feet, the town side at a free wz door-portal (type 6) in the map's
    /// return town. Recasting replaces the old pair.
    /// </summary>
    private async ValueTask SpawnDoorAsync(MapleSession session, Character c, SkillEffect effect)
    {
        Field field = _field!;
        FieldPlayer player = _player!;

        MapData? mapData = _maps.GetMap(c.MapId);
        int townMapId = mapData?.ReturnMap ?? 0;
        if (mapData is null || townMapId is <= 0 or MapData.NoLink || townMapId == c.MapId)
        {
            return; // no return town (already in town, or no data) — the door has nowhere to go
        }

        MapData? townData = _maps.GetMap(townMapId);
        if (townData is null)
        {
            return;
        }

        await RemoveDoorOfAsync(c.Id).ConfigureAwait(false);

        // The town side lands on a free door-portal spot (type 6); fall back to the spawn point.
        Field townField = _fields.Get(townMapId);
        var taken = new HashSet<int>(townField.Doors.Select(d => d.TownPortalId));
        PortalData? townSpot = townData.Portals.FirstOrDefault(p => p.Type == 6 && !taken.Contains(p.Id))
            ?? townData.SpawnPortal;
        if (townSpot is null)
        {
            return;
        }

        // Arriving on the field side spawns at the portal nearest the door.
        PortalData? nearest = mapData.Portals
            .OrderBy(p => Math.Abs(p.X - player.X) + Math.Abs(p.Y - player.Y))
            .FirstOrDefault();

        var door = new MysticDoor
        {
            OwnerId = c.Id,
            SkillId = MysticDoor.SkillMysticDoor,
            FieldMapId = c.MapId,
            FieldX = player.X,
            FieldY = player.Y,
            FieldPortalId = nearest?.Id ?? 0,
            TownMapId = townMapId,
            TownX = (short)townSpot.X,
            TownY = (short)townSpot.Y,
            TownPortalId = townSpot.Id,
            ExpiresAt = DateTime.UtcNow.AddMilliseconds(effect.DurationMs > 0 ? effect.DurationMs : 30_000),
        };
        field.AddDoor(door);
        townField.AddDoor(door);

        await field.BroadcastAsync(_packets.TownPortalCreated(c.Id, door.FieldX, door.FieldY, isTown: false)).ConfigureAwait(false);
        await townField.BroadcastAsync(_packets.TownPortalCreated(c.Id, door.TownX, door.TownY, isTown: true)).ConfigureAwait(false);
        await session.SendAsync(_packets.MysticDoorInfo(door)).ConfigureAwait(false);

        // The party window shows the door for every member.
        if (_parties.GetForCharacter(c.Id) is { } party)
        {
            byte[] notice = _packets.PartyTownPortalChanged(door);
            foreach (FieldPlayer member in party.Members)
            {
                await TrySendAsync(member, notice).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Removes a player's door pair from both maps and tells both fields.</summary>
    private async ValueTask RemoveDoorOfAsync(int ownerId)
    {
        MysticDoor? door = null;
        foreach (Field f in _fields.Fields)
        {
            door = f.FindDoorByOwner(ownerId);
            if (door is not null)
            {
                break;
            }
        }

        if (door is null)
        {
            return;
        }

        foreach (int mapId in new[] { door.FieldMapId, door.TownMapId })
        {
            Field side = _fields.Get(mapId);
            if (side.RemoveDoor(ownerId) is not null)
            {
                await side.BroadcastAsync(_packets.TownPortalRemoved(ownerId)).ConfigureAwait(false);
            }
        }

        // The party window drops the door.
        if (_parties.GetForCharacter(ownerId) is { } party)
        {
            byte[] notice = _packets.PartyTownPortalChanged(null);
            foreach (FieldPlayer member in party.Members)
            {
                await TrySendAsync(member, notice).ConfigureAwait(false);
            }
        }
    }
}
