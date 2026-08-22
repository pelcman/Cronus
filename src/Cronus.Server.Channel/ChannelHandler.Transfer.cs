// ChannelHandler partial: town portals, RPS, cash-shop/channel migration, summon relays.
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
    /// <summary>
    /// Handles <c>CP_EnterTownPortalRequest</c> — stepping through a Mystic Door (ports
    /// <c>ReqCTownPortalPool.TryEnterTownPortal</c>): warps to the door's other side.
    /// </summary>
    private async ValueTask HandleEnterTownPortalAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // [door owner id:4][unk:1]
        int doorOwnerId = packet.ReadInt();
        MysticDoor? door = _field.FindDoorByOwner(doorOwnerId);
        if (door is null)
        {
            return;
        }

        int target = door.TargetMapFor(_field.MapId);
        await MovePlayerToMapAsync(session, target, door.TargetPortalFor(_field.MapId)).ConfigureAwait(false);
    }

    // Janken (rock-paper-scissors) dialog constants (ports ReqCRPSGameDlg).
    private const int RpsTax = 1000;
    private const int RpsRefund = 500;
    private const int RpsFirstPrize = 4031332; // certificates for 1..10 straight wins

    /// <summary>
    /// Handles <c>CP_RPSGame</c> — the janken master's dialog (ports
    /// <c>ReqCRPSGameDlg.OnRPSGame</c>): 1000 meso a game, a first-round loss refunds 500,
    /// quitting cashes the streak out as the matching certificate item.
    /// </summary>
    private async ValueTask HandleRpsGameAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || packet.Remaining < 1)
        {
            return;
        }

        Character c = _player.Character;
        int type = packet.ReadByte();
        switch (type)
        {
            case 0: // start: pay the table charge
            {
                _player.RpsStreak = 0;
                if (c.Meso < RpsTax)
                {
                    await session.SendAsync(_packets.RpsResult(ChannelPackets.RpsNotEnoughMoney)).ConfigureAwait(false);
                    return;
                }

                c.Meso -= RpsTax;
                _characters.Save(c);
                await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
                await session.SendAsync(_packets.RpsResult(ChannelPackets.RpsStartGame)).ConfigureAwait(false);
                break;
            }

            case 1: // the player's hand: 0 rock, 1 paper, 2 scissors
            {
                if (packet.Remaining < 1)
                {
                    return;
                }

                int pick = packet.ReadByte();
                int npcPick = Random.Shared.Next(3);
                bool lose = (pick == 0 && npcPick == 1) || (pick == 1 && npcPick == 2) || (pick == 2 && npcPick == 0);
                bool refund = false;
                if (lose)
                {
                    refund = _player.RpsStreak == 0; // a first-round loss gives half back
                    _player.RpsStreak = -1;
                }
                else if (pick != npcPick)
                {
                    _player.RpsStreak++; // a draw replays the round (the reference counts it as a win)
                }

                await session.SendAsync(_packets.RpsSelection(npcPick, _player.RpsStreak)).ConfigureAwait(false);

                if (refund)
                {
                    c.Meso = (int)Math.Clamp((long)c.Meso + RpsRefund, 0, int.MaxValue);
                    _characters.Save(c);
                    await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
                }

                if (_player.RpsStreak >= 10)
                {
                    await ScriptGainItemAsync(session, RpsFirstPrize + 9, 1).ConfigureAwait(false);
                    await session.SendAsync(_packets.ShowItemGain(RpsFirstPrize + 9, 1)).ConfigureAwait(false);
                }

                break;
            }

            case 2: // ran out of time
                _player.RpsStreak = -1;
                await session.SendAsync(_packets.RpsResult(ChannelPackets.RpsTimeOver)).ConfigureAwait(false);
                break;

            case 3: // keep the streak going
                await session.SendAsync(_packets.RpsResult(ChannelPackets.RpsContinue)).ConfigureAwait(false);
                break;

            case 4: // quit: cash the streak out
            {
                await session.SendAsync(_packets.RpsResult(ChannelPackets.RpsQuit)).ConfigureAwait(false);
                if (_player.RpsStreak >= 1)
                {
                    int itemId = RpsFirstPrize + Math.Min(_player.RpsStreak, 10) - 1;
                    await ScriptGainItemAsync(session, itemId, 1).ConfigureAwait(false);
                    await session.SendAsync(_packets.ShowItemGain(itemId, 1)).ConfigureAwait(false);
                    _player.RpsStreak = 0;
                }

                break;
            }

            case 5: // retry (a fresh game after losing; charged on the next start)
                if (c.Meso < RpsTax)
                {
                    await session.SendAsync(_packets.RpsResult(ChannelPackets.RpsNotEnoughMoney)).ConfigureAwait(false);
                    return;
                }

                await session.SendAsync(_packets.RpsResult(ChannelPackets.RpsRetry)).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Handles <c>CP_UserMigrateToCashShopRequest</c> — sends the client to the cash-shop
    /// server (ports <c>OnUserMigrateToCashShopRequest</c>). Without one configured, decline so
    /// the button unfreezes.
    /// </summary>
    private async ValueTask HandleMigrateCashShopAsync(MapleSession session)
    {
        if (_player is null || _cashShopEndpoint is null || _player.Character.Hp <= 0)
        {
            await session.SendAsync(_packets.TransferChannelReqIgnored(reason: 2)).ConfigureAwait(false);
            return;
        }

        _characters.Save(_player.Character);
        await session.SendAsync(_packets.MigrateCommand(_cashShopEndpoint.Address, _cashShopEndpoint.Port)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserTransferChannelRequest</c> — a real channel change when this server runs
    /// several channels (ports <c>OnUserTransferChannelRequest</c>): persist, then hand the client
    /// the target channel's endpoint with <c>LP_MigrateCommand</c>. The client disconnects and
    /// migrates in there; this side's normal disconnect cleanup tears the old presence down.
    /// </summary>
    private async ValueTask HandleTransferChannelAsync(MapleSession session, PacketReader packet)
    {
        int target = packet.Remaining > 0 ? packet.ReadByte() : -1;
        if (_player is null || _channelEndpoints is null
            || target < 0 || target >= _channelEndpoints.Count || target == _channelId
            || _player.Character.Hp <= 0)
        {
            // Single-channel server / bad target / dead: decline so the channel menu unblocks.
            await session.SendAsync(_packets.TransferChannelReqIgnored(reason: 1)).ConfigureAwait(false);
            return;
        }

        _characters.Save(_player.Character);
        System.Net.IPEndPoint endpoint = _channelEndpoints[target];
        await session.SendAsync(_packets.MigrateCommand(endpoint.Address, endpoint.Port)).ConfigureAwait(false);
    }

    private async ValueTask HandleSummonedMoveAsync(PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_SummonedMove: [summonOid:4][raw CMovePath] — relayed verbatim like UserMove.
        int summonOid = packet.ReadInt();
        FieldSummon? summon = _field.FindSummon(summonOid);
        if (summon is null || summon.OwnerId != _player.Character.Id
            || summon.MoveAbility == SummonSkills.MoveStop)
        {
            return; // not theirs, or a stationary puppet (the reference rejects those moves too)
        }

        byte[] movePath = packet.ReadRemaining();
        await _field.BroadcastAsync(
            _packets.SummonedMove(summon, movePath),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
    }

    private async ValueTask HandleSummonedAttackAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_SummonedAttack (ports ReqCSummonedPool.OnAttack, the >= 164/186 branches):
        // [summonOid:4][skip:20][animation:1][skip:8][count:1][attack rect:8] then per hit
        // [mobOid:4][mobTemplateId:4][skip:15][damage:4].
        int summonOid = packet.ReadInt();
        FieldSummon? summon = _field.FindSummon(summonOid);
        if (summon is null || summon.OwnerId != _player.Character.Id || packet.Remaining < 38)
        {
            return;
        }

        packet.Skip(20);
        byte animation = packet.ReadByte();
        packet.Skip(8);
        int count = packet.ReadByte();
        packet.Skip(8);

        var hits = new List<(int MobObjectId, int Damage)>();
        for (int i = 0; i < count && packet.Remaining >= 27; i++)
        {
            int mobOid = packet.ReadInt();
            packet.ReadInt(); // mob template id
            packet.Skip(15);
            hits.Add((mobOid, DamageValidator.ClampLine(packet.ReadInt())));
        }

        await _field.BroadcastAsync(
            _packets.SummonedAttack(summon, animation, hits),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
        await ApplySummonDamageAsync(session, hits).ConfigureAwait(false);

        // Gaviota departs after its single strike.
        if (summon.SkillId == SummonSkills.Gaviota && _field.RemoveSummon(summon.ObjectId) is not null)
        {
            await _field.BroadcastAsync(_packets.SummonedLeaveField(summon, animated: true)).ConfigureAwait(false);
        }
    }

    /// <summary>Applies a summon's validated hits to the mobs (kill flow shared with player attacks).</summary>
    private async ValueTask ApplySummonDamageAsync(MapleSession session, IReadOnlyList<(int MobObjectId, int Damage)> hits)
    {
        foreach ((int mobOid, int damage) in hits)
        {
            FieldMob? mob = _field!.FindMob(mobOid);
            if (mob is null || mob.IsDead)
            {
                continue;
            }

            if (ZakumGate.IsBody(mob.TemplateId) && ZakumGate.BodyProtected(_field.Mobs))
            {
                continue; // the body ignores everything while an arm still stands
            }

            mob.Damage(damage);
            if (mob.IsBoss)
            {
                await _field.BroadcastAsync(_packets.MobHpTag(mob)).ConfigureAwait(false);
            }

            if (mob.IsDead)
            {
                mob.ControllerId = -1;
                mob.RespawnAtTick = MobRespawnService.NextRespawnTick(mob.MobTime);
                await _field.BroadcastAsync(_packets.MobLeaveField(mob.ObjectId)).ConfigureAwait(false);
                await GrantKillExpAsync(mob.Exp).ConfigureAwait(false);
                await UpdateQuestKillsAsync(session, mob.TemplateId).ConfigureAwait(false);
                await DropLootAsync(mob).ConfigureAwait(false);
                await SpawnRevivesAsync(mob).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask HandleSummonedHitAsync(PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_SummonedHit: [summonOid:4][attackAction:1][damage:4][mobTemplateIdFrom:4]
        // (ports ReqCSummonedPool.OnHit — only puppets take hits).
        int summonOid = packet.ReadInt();
        FieldSummon? summon = _field.FindSummon(summonOid);
        if (summon is null || summon.OwnerId != _player.Character.Id || !summon.IsPuppet)
        {
            return;
        }

        byte attackAction = packet.ReadByte();
        int damage = packet.ReadInt();
        int mobTemplateFrom = packet.ReadInt();

        summon.Hp -= damage;
        await _field.BroadcastAsync(
            _packets.SummonedHit(summon, attackAction, damage, mobTemplateFrom),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);

        if (summon.Hp <= 0 && _field.RemoveSummon(summon.ObjectId) is not null)
        {
            await _field.BroadcastAsync(_packets.SummonedLeaveField(summon, animated: true)).ConfigureAwait(false);
        }
    }
}
