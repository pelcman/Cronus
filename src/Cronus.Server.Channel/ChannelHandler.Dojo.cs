using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;

namespace Cronus.Server.Channel;

/// <summary>
/// 武陵道場 (Mu Lung Dojo): a solo tower of 38 boss stages. The entrance NPC (2091005 素公パンダ) and
/// the <c>dojang_*</c> portals call the <see cref="INpcPlayer"/> dojo hooks, which land here. The
/// tables and map arithmetic are in <see cref="MuLungDojo"/> (ported from the oracle's
/// <c>Event_DojoAgent</c>); this part does the live work: finding a free instance among the client's
/// 15 copies of each stage, warping, resetting and spawning, the training-points store, and the
/// per-stage clock. Entering a stage map spawns its boss and shows the clock and the start effect
/// (the oracle's <c>dojang_Eff</c> first-user hook), and the boss's death spawns the invincible
/// "cleared" checker (9300216, from the mob's wz <c>revive</c> — see <c>SpawnRevivesAsync</c>).
///
/// Not modelled yet, and marked <c>[DEV]</c> where the player meets them: party runs (925030000),
/// the vanquisher medal, and the special back-street floor (925040000).
/// </summary>
public sealed partial class ChannelHandler
{
    private System.Threading.Timer? _dojoTimer;

    /// <summary>The training points stored for this character (0 when none). The store is a quest
    /// record so it persists and rides the login blob, like the massacre's kill count.</summary>
    private int DojoPoints()
        => _player is not null && _player.Character.StartedQuests.TryGetValue(MuLungDojo.PointsQuest, out string? v)
           && int.TryParse(v, out int n) ? n : 0;

    private void SetDojoPointsValue(int points)
    {
        if (_player is null)
        {
            return;
        }

        points = Math.Max(0, points);
        _player.Character.StartedQuests[MuLungDojo.PointsQuest] = points.ToString();
        _characters.Save(_player.Character);
    }

    /// <summary>Persists the points and refreshes the client's on-screen "training points" HUD.</summary>
    private async ValueTask SetDojoPointsAsync(int points)
    {
        SetDojoPointsValue(points);
        if (_player is not null)
        {
            await _player.Session.SendAsync(_packets.QuestRecordExMessage(
                MuLungDojo.PointsQuest, $"pt={DojoPoints()};belt=0;tuto=1")).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// So Gong's "challenge" — find a free instance (a copy 0..14 whose whole tower is empty) and
    /// warp there. <paramref name="fromStage"/> 0 starts at stage 1; a resting-spot "continue"
    /// passes the resting stage so the next one opens. Party runs are declined here (the NPC shows
    /// the <c>[DEV]</c> notice). False when every instance is busy.
    /// </summary>
    private bool DojoEnter(bool party, int fromStage)
    {
        if (_player is null || party)
        {
            return false; // party dojo not wired yet ([DEV] in the NPC)
        }

        int stage = Math.Clamp(fromStage + 1, 1, MuLungDojo.MaxStage);
        int copy = FindFreeDojoCopy();
        if (copy < 0)
        {
            return false;
        }

        int target = MuLungDojo.SoloBase + (stage * 100) + copy;
        _fields.Get(target).ResetForEvent();
        _ = MovePlayerToMapAsync(_player.Session, target, spawnPortal: 0);
        return true;
    }

    /// <summary>The lowest copy index (0..14) whose every stage map is empty, or -1 when all are busy.</summary>
    private int FindFreeDojoCopy()
    {
        for (int copy = 0; copy < MuLungDojo.Copies; copy++)
        {
            bool free = true;
            for (int stage = 1; stage <= MuLungDojo.MaxStage && free; stage++)
            {
                if (_fields.Get(MuLungDojo.SoloBase + (stage * 100) + copy).Players.Count > 0)
                {
                    free = false;
                }
            }

            if (free)
            {
                return copy;
            }
        }

        return -1;
    }

    /// <summary>
    /// The <c>dojang_next</c> portal: award this stage's points and advance. The last stage (38)
    /// pays out exp and drops the player on the rooftop; otherwise the next stage map opens (its
    /// boss and clock come from the entry hook).
    /// </summary>
    private async ValueTask DojoNextStageAsync()
    {
        if (_player is null)
        {
            return;
        }

        int here = _player.Character.MapId;
        int stage = MuLungDojo.StageOf(here);
        if (stage == 0)
        {
            return;
        }

        // The boss stage must be cleared to advance (the invincible checker marks it) — the client's
        // door reactor gates the same way. The player cannot reach this portal otherwise.
        if (_field is not null && MuLungDojo.MobForStage(stage) != 0 && _field.CountMobs(MuLungDojo.ClearChecker) == 0)
        {
            return;
        }

        int award = MuLungDojo.PointsForStage(stage);
        if (award > 0)
        {
            int total = DojoPoints() + award;
            await SetDojoPointsAsync(total).ConfigureAwait(false);
            await ReplyAsync(_player.Session, $"修練点数を {award} 点獲得しました。総修練点数は {total} 点です。").ConfigureAwait(false);
        }

        if (stage >= MuLungDojo.MaxStage)
        {
            // Cleared the tower. The oracle pays 5000 cash points here; without a wz-verified dojo
            // clear reward we grant exp scaled by the run (Cosmic's 2000×points) and note it.
            await GrantKillExpAsync(2000 * Math.Max(1, DojoPoints())).ConfigureAwait(false); // [DEV] 創作: clear reward
            CancelDojoTimer();
            await MovePlayerToMapAsync(_player.Session, MuLungDojo.Roof, spawnPortal: 0).ConfigureAwait(false);
            return;
        }

        int next = MuLungDojo.StageMap(here, stage + 1);
        _fields.Get(next).ResetForEvent();
        await MovePlayerToMapAsync(_player.Session, next, spawnPortal: 0).ConfigureAwait(false);
    }

    /// <summary>The <c>dojang_up</c> portal: once the stage is cleared (the invincible checker is on
    /// the map) lift the player up to the exit portal. False while the boss still lives.</summary>
    private async ValueTask<bool> DojoTeleportUpAsync()
    {
        if (_player is null || _field is null || _field.CountMobs(MuLungDojo.ClearChecker) == 0)
        {
            return false;
        }

        await _player.Session.SendAsync(_packets.UserEffectLocal(ChannelPackets.UserEffectPlayPortalSE)).ConfigureAwait(false);
        await _player.Session.SendAsync(_packets.UserTeleport(6)).ConfigureAwait(false);
        return true;
    }

    /// <summary>The <c>dojang_exit</c> portal: leave the dojo. The oracle returns to the player's
    /// saved "MIRROR" town, which Cronus does not track yet, so this uses ヘネシス (Henesys) (100000000).</summary>
    private async ValueTask DojoExitAsync()
    {
        if (_player is null)
        {
            return;
        }

        CancelDojoTimer();
        await MovePlayerToMapAsync(_player.Session, 100000000, spawnPortal: 0).ConfigureAwait(false); // [DEV] 創作: no saved return town
    }

    /// <summary>The <c>dojang_tuto</c> portal: leaving the tutorial map once its agent is beaten
    /// drops the player at So Gong's challenge room. False while the agent still lives.</summary>
    private async ValueTask<bool> DojoTutorialExitAsync()
    {
        if (_player is null || _field is null || _field.CountMobs(MuLungDojo.ClearChecker) == 0)
        {
            return false;
        }

        await MovePlayerToMapAsync(_player.Session, MuLungDojo.Entrance, spawnPortal: 0).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// The oracle's <c>dojang_Eff</c> first-user hook plus the stage's monster spawn: entering a
    /// boss stage shows the countdown clock and the start effect and spawns the stage boss; leaving
    /// every dojo map stops the clock. Resting stages (6, 12, …) have no boss and no clock — So Gong
    /// waits there instead. Called from <see cref="OnFieldEnteredAsync"/>.
    /// </summary>
    private async ValueTask OnDojoFieldEnteredAsync(int mapId)
    {
        if (_player is null)
        {
            return;
        }

        if (!MuLungDojo.IsDojo(mapId))
        {
            CancelDojoTimer();
            return;
        }

        int stage = MuLungDojo.StageOf(mapId);
        int boss = MuLungDojo.MobForStage(stage);
        if (MuLungDojo.IsTutorialMap(mapId))
        {
            boss = MuLungDojo.TutorialMob;
            stage = 1;
        }

        if (boss == 0)
        {
            CancelDojoTimer(); // a resting spot / the hall: no fight here
            return;
        }

        // Spawn the boss (its wz revive list carries the invincible 9300216 checker that marks the
        // stage cleared on death), then the countdown clock and the start effect. This hook runs
        // while the player is still being switched into the field, so the boss goes into the target
        // map's field and its appear packet is sent straight to the (solo) player.
        await DojoSpawnBossAsync(mapId, boss).ConfigureAwait(false);

        int seconds = MuLungDojo.ClockSeconds(stage);
        await _player.Session.SendAsync(_packets.ClockCountdown(seconds)).ConfigureAwait(false);
        await _player.Session.SendAsync(_packets.FieldEffectSound("Dojang/start")).ConfigureAwait(false);
        await _player.Session.SendAsync(_packets.FieldEffectScreen("dojang/start/stage")).ConfigureAwait(false);
        await _player.Session.SendAsync(_packets.FieldEffectScreen($"dojang/start/number/{MuLungDojo.DisplayStage(stage)}")).ConfigureAwait(false);
        await _player.Session.SendAsync(_packets.FieldEffectTremble()).ConfigureAwait(false);

        StartDojoTimer(seconds);
    }

    /// <summary>
    /// Puts the stage boss into the target map's field and shows it to the solo player directly
    /// (the field-entry hook runs before the player has joined the field, so a plain broadcast
    /// would miss them). The player controls it, so its AI runs. It spawns at the player's entry
    /// position; the oracle uses fixed foothold points, which the wz does not expose here.
    /// </summary>
    private async ValueTask DojoSpawnBossAsync(int mapId, int bossId)
    {
        if (_player is null)
        {
            return;
        }

        Field field = _fields.Get(mapId);
        MobData? stats = _fields.MobProvider?.GetMob(bossId);
        FieldMob mob = field.SpawnMob(bossId, stats, (short)_player.X, (short)_player.Y, foothold: 0);
        mob.ControllerId = _player.Character.Id;
        await _player.Session.SendAsync(_packets.MobEnterField(mob)).ConfigureAwait(false);
        await _player.Session.SendAsync(_packets.MobChangeController(mob, aggro: true)).ConfigureAwait(false);
        await field.BroadcastAsync(_packets.MobEnterField(mob), exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
    }

        /// <summary>When the stage clock runs out the player is sent to the exit hall (event
    /// management; the oracle relies on the client clock, Cosmic warps out — we warp out).</summary>
    private void StartDojoTimer(int seconds)
    {
        CancelDojoTimer();
        _dojoTimer = new System.Threading.Timer(_ => _ = DojoTimeUpAsync(), null, seconds * 1000L, System.Threading.Timeout.Infinite);
    }

    private void CancelDojoTimer()
    {
        _dojoTimer?.Dispose();
        _dojoTimer = null;
    }

    private async Task DojoTimeUpAsync()
    {
        try
        {
            if (_player is not null && MuLungDojo.IsDojo(_player.Character.MapId))
            {
                await MovePlayerToMapAsync(_player.Session, MuLungDojo.Exit, spawnPortal: 0).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[dojo] time-up warp failed: {ex.Message}");
        }
    }
}
