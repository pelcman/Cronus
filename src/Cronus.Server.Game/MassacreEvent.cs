namespace Cronus.Server.Game;

/// <summary>
/// What the Massacre event needs from the channel — the calls the oracle's
/// <c>Event_PyramidSubway</c> makes on <c>MapleCharacter</c> / <c>MapleMap</c>, narrowed to what the
/// event really uses so the logic can run against a fake in tests.
/// </summary>
public interface IMassacreHost
{
    /// <summary>The owner's current map.</summary>
    int MapId { get; }

    /// <summary>Members in the owner's party (0 = no party).</summary>
    int PartySize { get; }

    /// <summary>True when the owner leads the party, or has none.</summary>
    bool IsPartyLeader { get; }

    ChannelPackets Packets { get; }

    /// <summary>To the owner only (the oracle's <c>broadcastEnergy</c>).</summary>
    ValueTask SendAsync(byte[] packet);

    /// <summary>To the owner and every party member on the owner's map (<c>broadcastUpdate</c>).</summary>
    ValueTask SendToPartyInMapAsync(byte[] packet);

    /// <summary>
    /// The oracle's <c>changeMap</c>: party members on the owner's map whose level lies in
    /// [<paramref name="minLevel"/>, <paramref name="maxLevel"/>] get the screen effect (if any)
    /// and the warp to portal 0, then the owner does.
    /// </summary>
    ValueTask WarpPartyAsync(int mapId, int minLevel, int maxLevel, string? screenEffect);

    /// <summary>The owner alone, portal 0 (the bonus stages are solo).</summary>
    ValueTask WarpOwnerAsync(int mapId);

    int PlayersOn(int mapId);

    /// <summary>The oracle's <c>clearMap</c> / <c>resetFully</c>: every mob back alive, drops gone.</summary>
    void ResetMap(int mapId);

    /// <summary><c>map.respawn(true)</c> on the owner's map: dead mobs come back now.</summary>
    void ForceRespawn();

    int CountMobs(int templateId);

    ValueTask SpawnMobAtOwnerAsync(int templateId);

    ValueTask GainExpAsync(int exp);

    string? GetQuestData(int questId);

    void SetQuestData(int questId, string data);
}

/// <summary>
/// The Kerning Subway / Nett's Pyramid "massacre" mini-game — a port of the oracle's
/// <c>odin.server.maps.Event_PyramidSubway</c>. One instance per character on a massacre map
/// (the map's <c>onUserEnter</c> is <c>Massacre_first</c>); only the party leader (or a solo player)
/// runs the timers: the energy bar drains every second (−5 solo, −10 in a party) and each kill
/// refills it (+5, misses −5); when it hits 0 the party fails back to the lobby. Each stage has a
/// countdown (subway 3 min; pyramid 4 min then 5) after which the leader's warp to the next stage
/// happens, and clearing the last stage warps to the result map, which ranks the total kills and
/// pays exp (<see cref="RankFor"/>, <see cref="PointsFor"/>). The client draws all of this from
/// the packets this class sends: the countdown clock, the "killing/…" screen effects, the six
/// <c>massacre_*</c> session values and the gauge.
/// </summary>
public sealed class MassacreEvent : IDisposable
{
    public const int SubwayFirstStage = 910320100;
    public const int SubwayLastStageMap = 910320304;
    public const int SubwayLobby = 910320001;
    public const int SubwayResult = 910330001;
    public const int SubwayBonus = 910320010;
    public const int PyramidFirstStage = 926010100;
    public const int PyramidLastStageMap = 926013504;
    public const int PyramidLobby = 926010001;
    public const int PyramidResult = 926020001;
    public const int PyramidBonus = 926010010;
    public const int SubwayQuestRecord = 7662;
    public const int PyramidQuestRecord = 7760;
    public const int Yeti = 9300021;
    public const int Instances = 5;
    public const int BonusInstances = 20;

    /// <summary>The wz <c>info/fieldType</c> of a massacre stage and of its result map.</summary>
    public const int FieldTypeMassacre = 23;
    public const int FieldTypeMassacreResult = 24;

    private readonly IMassacreHost _host;
    private readonly Random _rng;
    private readonly int _type;   // -1 = subway, 0..3 = pyramid difficulty
    private int _kill, _cool, _miss, _skill;
    private int _energy = 100;
    private bool _tickedOnce;
    private Timer? _energyTimer;
    private Timer? _stageTimer;
    private Timer? _yetiTimer;
    private bool _disposed;

    public MassacreEvent(IMassacreHost host, int mapId, Random? rng = null)
    {
        _host = host;
        _rng = rng ?? Random.Shared;
        _type = TypeOf(mapId);
    }

    public int Kills => _kill;
    public int Cool => _cool;
    public int Misses => _miss;
    public int Skills => _skill;
    public int Energy => _energy;
    public int Type => _type;
    public bool IsSubway => _type == -1;
    public bool IsDisposed => _disposed;

    /// <summary>-1 for the subway maps (91032xxxx), else the pyramid difficulty digit.</summary>
    public static int TypeOf(int mapId) => mapId / 10000 == 91032 ? -1 : mapId % 10000 / 1000;

    public static bool IsStageMap(int mapId)
        => (mapId >= SubwayFirstStage && mapId <= SubwayLastStageMap)
        || (mapId >= PyramidFirstStage && mapId <= PyramidLastStageMap);

    /// <summary>The stage countdown in seconds (the oracle's <c>time</c>: 180 / 240 / 300, minus one).</summary>
    public int StageSeconds(int stage) => (_type == -1 ? 180 : (stage == 1 ? 240 : 300)) - 1;

    /// <summary>
    /// The oracle constructor's work: a solo player or the party leader starts stage 1 and the
    /// one-second energy drain; everyone else just holds the counters until the leader's updates
    /// reach them.
    /// </summary>
    public async ValueTask StartAsync()
    {
        if (_host.PartySize <= 1 || _host.IsPartyLeader)
        {
            await CommenceStageAsync(1).ConfigureAwait(false);
            _energyTimer = new Timer(_ => _ = EnergyTickAsync(), null, 1000, 1000);
        }
    }

    private async Task EnergyTickAsync()
    {
        try
        {
            if (_disposed)
            {
                return;
            }

            _energy -= _host.PartySize > 1 ? 10 : 5;
            if (_tickedOnce)
            {
                _host.ForceRespawn();
            }
            else
            {
                _tickedOnce = true;
            }

            if (_energy <= 0)
            {
                await FailAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[massacre] energy tick: {ex.Message}");
        }
    }

    /// <summary>The oracle's <c>commenceTimerNextMap</c>: countdown, the three stage effects, a full update, yetis on pyramid stages 4–5, and the stage timer.</summary>
    public async ValueTask CommenceStageAsync(int stage)
    {
        CancelStageTimers();
        int time = StageSeconds(stage);
        ChannelPackets p = _host.Packets;
        await _host.SendToPartyInMapAsync(p.ClockCountdown(time)).ConfigureAwait(false);
        await _host.SendToPartyInMapAsync(p.FieldEffectScreen($"killing/first/number/{stage}")).ConfigureAwait(false);
        await _host.SendToPartyInMapAsync(p.FieldEffectScreen("killing/first/stage")).ConfigureAwait(false);
        await _host.SendToPartyInMapAsync(p.FieldEffectScreen("killing/first/start")).ConfigureAwait(false);
        await FullUpdateAsync(stage).ConfigureAwait(false);

        if (_type != -1 && stage is 4 or 5)
        {
            int keep = stage == 4 ? 1 : 2;
            _yetiTimer = new Timer(_ =>
            {
                if (!_disposed && _host.CountMobs(Yeti) <= keep)
                {
                    _ = _host.SpawnMobAtOwnerAsync(Yeti);
                }
            }, null, 10_000, 10_000);
        }

        _stageTimer = new Timer(_ => _ = StageTimeUpAsync(), null, time * 1000L, Timeout.Infinite);
    }

    private async Task StageTimeUpAsync()
    {
        try
        {
            if (_disposed)
            {
                return;
            }

            bool moved = _type == -1
                ? await WarpNextSubwayAsync().ConfigureAwait(false)
                : await WarpNextPyramidAsync().ConfigureAwait(false);
            if (!moved)
            {
                await FailAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[massacre] stage timer: {ex.Message}");
        }
    }

    /// <summary>The six <c>massacre_*</c> session values the client's gauge UI reads, then the gauge itself.</summary>
    public async ValueTask FullUpdateAsync(int stage)
    {
        int party = _host.PartySize;
        await EnergyAsync("massacre_party", party).ConfigureAwait(false);
        await EnergyAsync("massacre_miss", _miss).ConfigureAwait(false);
        await EnergyAsync("massacre_cool", _cool).ConfigureAwait(false);
        await EnergyAsync("massacre_skill", _skill).ConfigureAwait(false);
        await EnergyAsync("massacre_laststage", stage - 1).ConfigureAwait(false);
        await EnergyAsync("massacre_hit", _kill).ConfigureAwait(false);
        await BroadcastGaugeAsync().ConfigureAwait(false);
    }

    private ValueTask EnergyAsync(string key, int value)
        => _host.SendAsync(_host.Packets.SessionValue(key, value.ToString()));

    private ValueTask BroadcastGaugeAsync()
        => _host.SendToPartyInMapAsync(_host.Packets.MassacreIncGauge(_energy));

    /// <summary>A mob of the owner's died on a massacre map.</summary>
    public async ValueTask OnKillAsync()
    {
        _kill++;
        if (_rng.Next(100) < 5)
        {
            // The oracle's placeholder for the mob's coolDamage / coolDamageProb.
            _cool++;
            await EnergyAsync("massacre_cool", _cool).ConfigureAwait(false);
        }

        _energy = Math.Min(100, _energy + 5);

        if (_type != -1)
        {
            for (int i = 5; i >= 1; i--)
            {
                if ((_kill + _cool) % (i * 100) == 0 && _rng.Next(100) < 50)
                {
                    await _host.SendAsync(_host.Packets.FieldEffectScreen($"killing/yeti{i - 1}")).ConfigureAwait(false);
                    break;
                }
            }

            if ((_kill + _cool) % 500 == 0)
            {
                _skill++;
                await EnergyAsync("massacre_skill", _skill).ConfigureAwait(false);
            }
        }

        await BroadcastGaugeAsync().ConfigureAwait(false);
        await EnergyAsync("massacre_hit", _kill).ConfigureAwait(false);
    }

    /// <summary>An attack that hit nothing.</summary>
    public async ValueTask OnMissAsync()
    {
        _miss++;
        _energy -= 5;
        await BroadcastGaugeAsync().ConfigureAwait(false);
        await EnergyAsync("massacre_miss", _miss).ConfigureAwait(false);
    }

    /// <summary>A skill was cast; on the pyramid a stored skill charge is spent. Returns whether one was available.</summary>
    public async ValueTask<bool> OnSkillUseAsync()
    {
        if (_skill > 0 && _type != -1)
        {
            _skill--;
            await EnergyAsync("massacre_skill", _skill).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    /// <summary>The owner arrived on another map: the result map succeeds, a map outside the run ends it, the next stage restarts the clock.</summary>
    public async ValueTask OnChangeMapAsync(int newMapId)
    {
        if ((_type == -1 && newMapId == SubwayResult) || (_type != -1 && newMapId == PyramidResult + _type))
        {
            await SucceedAsync().ConfigureAwait(false);
            return;
        }

        bool outside = _type == -1
            ? newMapId < SubwayFirstStage || newMapId > SubwayLastStageMap
            : newMapId < PyramidFirstStage || newMapId > PyramidLastStageMap;
        if (outside)
        {
            Dispose();
            return;
        }

        if (_host.PartySize <= 1 || _host.IsPartyLeader)
        {
            _energy = 100;
            await CommenceStageAsync(newMapId % 1000 / 100).ConfigureAwait(false);
        }
    }

    /// <summary>Rank 0 (best) to 3 by total kills, 4 = unranked. Subway: 2000/1500/1000/500; pyramid: 3000/2000/1500/500.</summary>
    public static byte RankFor(int type, int totalKills)
    {
        if (type == -1)
        {
            return totalKills >= 2000 ? (byte)0 : totalKills >= 1500 ? (byte)1 : totalKills >= 1000 ? (byte)2 : totalKills >= 500 ? (byte)3 : (byte)4;
        }

        return totalKills >= 3000 ? (byte)0 : totalKills >= 2000 ? (byte)1 : totalKills >= 1500 ? (byte)2 : totalKills >= 500 ? (byte)3 : (byte)4;
    }

    /// <summary>The oracle's bonus-point table by pyramid difficulty (the subway is the default row) and rank.</summary>
    public static int PointsFor(int type, byte rank)
    {
        int[] row = type switch
        {
            0 => new[] { 60500, 55000, 46750, 22000 },
            1 => new[] { 66000, 60000, 51750, 24000 },
            2 => new[] { 71500, 65000, 55250, 26000 },
            3 => new[] { 77000, 70000, 59500, 28000 },
            _ => new[] { 22000, 17000, 10750, 7000 },
        };
        return rank < 4 ? row[rank] : 0;
    }

    /// <summary>The oracle's <c>succeed</c>: kills go into the quest record, the rank pays exp, the result screen shows.</summary>
    public async ValueTask SucceedAsync()
    {
        int questId = _type == -1 ? SubwayQuestRecord : PyramidQuestRecord;
        int sofar = int.TryParse(_host.GetQuestData(questId), out int n) ? n : 0;
        int total = _kill + _cool;
        _host.SetQuestData(questId, (sofar + total).ToString());

        byte rank = RankFor(_type, total);
        int exp = 0;
        if (rank < 4)
        {
            exp = (_kill * 2) + (_cool * 10) + PointsFor(_type, rank);
            await _host.GainExpAsync(exp).ConfigureAwait(false); // the host applies the server exp rate, like the oracle's getExpRate()
        }

        await _host.SendAsync(_host.Packets.FieldEffectScreen("killing/clear")).ConfigureAwait(false);
        await _host.SendAsync(_host.Packets.MassacreResult(rank, exp)).ConfigureAwait(false);
        Dispose();
    }

    /// <summary>The oracle's <c>fail</c>: everyone back to the lobby with the "fail" screen, then the run ends.</summary>
    public async ValueTask FailAsync()
    {
        int lobby = _type == -1 ? SubwayLobby : PyramidLobby + _type;
        await _host.WarpPartyAsync(lobby, 1, 200, "killing/fail").ConfigureAwait(false);
        Dispose();
    }

    private void CancelStageTimers()
    {
        _stageTimer?.Dispose();
        _stageTimer = null;
        _yetiTimer?.Dispose();
        _yetiTimer = null;
    }

    /// <summary>
    /// Ends the run and stops the timers. As in the oracle, a leader who drops out of a party run
    /// takes the party back to the lobby (the warp only; the members' own events end when they
    /// arrive there).
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        bool lead = _energyTimer is not null && _stageTimer is not null;
        _energyTimer?.Dispose();
        _energyTimer = null;
        CancelStageTimers();
        if (lead && _host.PartySize > 1)
        {
            int lobby = _type == -1 ? SubwayLobby : PyramidLobby + _type;
            _ = _host.WarpPartyAsync(lobby, 1, 200, "killing/fail");
        }
    }

    // ----- instance search and stage warps (the oracle's static warp* helpers) -------------------

    /// <summary>Puts a level 25–30 party into a free subway stage-1 instance. False when all five are busy.</summary>
    public static async ValueTask<bool> WarpStartSubwayAsync(IMassacreHost host)
    {
        for (int i = 0; i < Instances; i++)
        {
            int map = SubwayFirstStage + i;
            if (host.PlayersOn(map) == 0)
            {
                host.ResetMap(map);
                await host.WarpPartyAsync(map, 25, 30, null).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    /// <summary>A free 999 bonus carriage for the owner alone.</summary>
    public static async ValueTask<bool> WarpBonusSubwayAsync(IMassacreHost host)
    {
        for (int i = 0; i < BonusInstances; i++)
        {
            int map = SubwayBonus + i;
            if (host.PlayersOn(map) == 0)
            {
                host.ResetMap(map);
                await host.WarpOwnerAsync(map).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    /// <summary>A free pyramid stage-1 instance for the difficulty (level bands 40–60 / 45–60 / 50–60 / 61–200).</summary>
    public static async ValueTask<bool> WarpStartPyramidAsync(IMassacreHost host, int difficulty)
    {
        int first = PyramidFirstStage + (difficulty * 1000);
        (int minLevel, int maxLevel) = difficulty switch
        {
            1 => (45, 60),
            2 => (50, 60),
            3 => (61, 200),
            _ => (40, 60),
        };
        for (int i = 0; i < Instances; i++)
        {
            int map = first + i;
            if (host.PlayersOn(map) == 0)
            {
                host.ResetMap(map);
                await host.WarpPartyAsync(map, minLevel, maxLevel, null).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    public static async ValueTask<bool> WarpBonusPyramidAsync(IMassacreHost host, int difficulty)
    {
        int first = PyramidBonus + (difficulty * 20);
        for (int i = 0; i < BonusInstances; i++)
        {
            int map = first + i;
            if (host.PlayersOn(map) == 0)
            {
                host.ResetMap(map);
                await host.WarpOwnerAsync(map).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    private async ValueTask<bool> WarpNextSubwayAsync()
    {
        int stage = (_host.MapId - SubwayFirstStage) / 100;
        if (stage >= 2)
        {
            await _host.WarpPartyAsync(SubwayResult, 1, 200, "killing/clear").ConfigureAwait(false);
            return true;
        }

        int next = SubwayFirstStage + ((stage + 1) * 100);
        for (int i = 0; i < Instances; i++)
        {
            if (_host.PlayersOn(next + i) == 0)
            {
                _host.ResetMap(next + i);
                await _host.WarpPartyAsync(next + i, 1, 200, "killing/clear").ConfigureAwait(false); // any level: they may have levelled
                return true;
            }
        }

        return false;
    }

    private async ValueTask<bool> WarpNextPyramidAsync()
    {
        int first = PyramidFirstStage + (_type * 1000);
        int stage = (_host.MapId - first) / 100;
        if (stage >= 4)
        {
            await _host.WarpPartyAsync(PyramidResult + _type, 1, 200, "killing/clear").ConfigureAwait(false);
            return true;
        }

        int next = first + ((stage + 1) * 100);
        for (int i = 0; i < Instances; i++)
        {
            if (_host.PlayersOn(next + i) == 0)
            {
                _host.ResetMap(next + i);
                await _host.WarpPartyAsync(next + i, 1, 200, "killing/clear").ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }
}
