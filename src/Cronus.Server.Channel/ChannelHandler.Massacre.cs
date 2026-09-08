using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Server.Game;

namespace Cronus.Server.Channel;

/// <summary>
/// The Kerning Subway / Nett's Pyramid massacre mini-game, wired to this session: the handler is
/// the event's <see cref="IMassacreHost"/> (owner, party, field, packets), and the field-entry,
/// combat, skill and disconnect paths feed the event. Before this existed, entering a massacre
/// stage (wz <c>info/fieldType</c> 23, <c>onUserEnter</c> "Massacre_first") crashed the client a
/// few seconds after loading: its gauge UI expects the clock, the "killing/…" screen effects, the
/// six <c>massacre_*</c> session values and the gauge that the oracle's <c>Event_PyramidSubway</c>
/// sends on entry — found by the map sweep (docs/TASK.md フェーズ0, crash #1).
/// </summary>
public sealed partial class ChannelHandler : IMassacreHost
{
    /// <summary>This character's massacre run, while one is going.</summary>
    private MassacreEvent? _massacre;

    int IMassacreHost.MapId => _player?.Character.MapId ?? 0;

    int IMassacreHost.PartySize => _player is null ? 0 : _parties.GetForCharacter(_player.Character.Id)?.Members.Count ?? 0;

    bool IMassacreHost.IsPartyLeader
        => _player is null || _parties.GetForCharacter(_player.Character.Id) is not { } party || party.IsLeader(_player.Character.Id);

    ChannelPackets IMassacreHost.Packets => _packets;

    ValueTask IMassacreHost.SendAsync(byte[] packet) => _player?.Session.SendAsync(packet) ?? default;

    async ValueTask IMassacreHost.SendToPartyInMapAsync(byte[] packet)
    {
        foreach (FieldPlayer member in PartyOnMyMap())
        {
            await member.Session.SendAsync(packet).ConfigureAwait(false);
        }
    }

    async ValueTask IMassacreHost.WarpPartyAsync(int mapId, int minLevel, int maxLevel, string? screenEffect)
    {
        if (_player is null)
        {
            return;
        }

        foreach (FieldPlayer member in PartyOnMyMap())
        {
            if (member == _player || member.Character.Level < minLevel || member.Character.Level > maxLevel)
            {
                continue;
            }

            if (screenEffect is not null)
            {
                await member.Session.SendAsync(_packets.FieldEffectScreen(screenEffect)).ConfigureAwait(false);
            }

            if (member.WarpAsync is { } warp)
            {
                await warp(mapId, 0).ConfigureAwait(false);
            }
        }

        if (screenEffect is not null)
        {
            await _player.Session.SendAsync(_packets.FieldEffectScreen(screenEffect)).ConfigureAwait(false);
        }

        await MovePlayerToMapAsync(_player.Session, mapId, spawnPortal: 0).ConfigureAwait(false);
    }

    ValueTask IMassacreHost.WarpOwnerAsync(int mapId)
        => _player is null ? default : MovePlayerToMapAsync(_player.Session, mapId, spawnPortal: 0);

    int IMassacreHost.PlayersOn(int mapId) => _fields.Get(mapId).Players.Count;

    void IMassacreHost.ResetMap(int mapId) => _fields.Get(mapId).ResetForEvent();

    void IMassacreHost.ForceRespawn() => _field?.RespawnDeadNow(Environment.TickCount64);

    int IMassacreHost.CountMobs(int templateId) => _field?.CountMobs(templateId) ?? 0;

    async ValueTask IMassacreHost.SpawnMobAtOwnerAsync(int templateId)
    {
        if (_field is null || _player is null)
        {
            return;
        }

        MobData? stats = _fields.MobProvider?.GetMob(templateId);
        FieldMob mob = _field.SpawnMob(templateId, stats, (short)_player.X, (short)_player.Y, 0);
        await _field.BroadcastAsync(_packets.MobEnterField(mob)).ConfigureAwait(false);
        mob.ControllerId = _player.Character.Id;
        await _player.Session.SendAsync(_packets.MobChangeController(mob, aggro: true)).ConfigureAwait(false);
    }

    ValueTask IMassacreHost.GainExpAsync(int exp) => GrantKillExpAsync(exp);

    string? IMassacreHost.GetQuestData(int questId)
        => _player is not null && _player.Character.StartedQuests.TryGetValue(questId, out string? data) ? data : null;

    void IMassacreHost.SetQuestData(int questId, string data)
    {
        if (_player is null)
        {
            return;
        }

        _player.Character.StartedQuests[questId] = data;
        _characters.Save(_player.Character);
    }

    /// <summary>The owner plus the party members standing on the owner's map (just the owner without a party).</summary>
    private IEnumerable<FieldPlayer> PartyOnMyMap()
    {
        if (_player is null)
        {
            yield break;
        }

        Party? party = _parties.GetForCharacter(_player.Character.Id);
        if (party is null || party.Members.Count <= 1)
        {
            yield return _player;
            yield break;
        }

        bool ownerSeen = false;
        foreach (FieldPlayer member in party.Members)
        {
            if (member.Character.MapId != _player.Character.MapId)
            {
                continue;
            }

            ownerSeen |= member == _player;
            yield return member;
        }

        if (!ownerSeen)
        {
            yield return _player;
        }
    }

    /// <summary>
    /// Runs after the client has been placed on a field (login and every map change). A massacre
    /// stage starts (or continues) the run; the result map ends it; any other map disposes it. The
    /// oracle's <c>Massacre_result</c> map script always shows "killing/fail" — here only when no
    /// run led to the result map, so a clear is not followed by a failure banner (intentional
    /// deviation from the oracle's placeholder).
    /// </summary>
    private async ValueTask OnFieldEnteredAsync(int mapId)
    {
        MapData? map = _maps.GetMap(mapId);
        bool massacreStage = map is not null && (map.OnUserEnter == "Massacre_first" || map.FieldType == MassacreEvent.FieldTypeMassacre);
        bool hadRun = _massacre is not null;

        if (massacreStage)
        {
            // A live run follows the player (next stage → new clock); a run that ended (the
            // result map, or a stage outside its range) is replaced, exactly as the oracle's
            // Massacre_first creates a fresh Event_PyramidSubway whenever none is attached. The
            // second sweep crash (910330200, fieldType 23 with an empty onUserEnter) was a
            // disposed run left attached: no packets went out and the gauge UI died.
            if (_massacre is { IsDisposed: false })
            {
                await _massacre.OnChangeMapAsync(mapId).ConfigureAwait(false);
            }

            if (_massacre is null || _massacre.IsDisposed)
            {
                _massacre = new MassacreEvent(this, mapId);
                await _massacre.StartAsync().ConfigureAwait(false);
            }

            return;
        }

        if (_massacre is not null)
        {
            await _massacre.OnChangeMapAsync(mapId).ConfigureAwait(false);
            if (_massacre.IsDisposed)
            {
                _massacre = null;
            }
        }

        if (map?.OnUserEnter == "Massacre_result" && !hadRun && _player is not null)
        {
            await _player.Session.SendAsync(_packets.FieldEffectScreen("killing/fail")).ConfigureAwait(false);
        }
    }

    /// <summary>The NPC's "enter the platform": a free stage-1 instance for the owner's party.</summary>
    private bool TryStartSubwayMassacre()
        => MassacreEvent.WarpStartSubwayAsync(this).AsTask().GetAwaiter().GetResult();

    /// <summary>The NPC's "999 carriage": a free bonus instance for the owner alone.</summary>
    private bool TryEnterSubwayBonus()
        => MassacreEvent.WarpBonusSubwayAsync(this).AsTask().GetAwaiter().GetResult();

    private void EndMassacreOnDisconnect()
    {
        _massacre?.Dispose();
        _massacre = null;
    }
}
