using Cronus.Domain;
using Cronus.Network;

namespace Cronus.Server.Channel;

/// <summary>A player present in a field: the character plus their live session and position.</summary>
public sealed class FieldPlayer
{
    public FieldPlayer(Character character, MapleSession session)
    {
        Character = character;
        Session = session;
        X = 0;
        Y = 0;
    }

    public Character Character { get; }

    public MapleSession Session { get; }

    public short X { get; set; }

    public short Y { get; set; }

    public byte Stance { get; set; }
}

/// <summary>
/// One map instance: the players inside it and broadcast helpers (ports the broadcast role of
/// <c>odin.server.maps.MapleMap</c>, minus map data which arrives with Cronus.Data).
/// </summary>
public sealed class Field
{
    private readonly Dictionary<int, FieldPlayer> _players = new();
    private readonly object _gate = new();

    public Field(int mapId) => MapId = mapId;

    public int MapId { get; }

    public IReadOnlyList<FieldPlayer> Players
    {
        get
        {
            lock (_gate)
            {
                return _players.Values.ToList();
            }
        }
    }

    public void Enter(FieldPlayer player)
    {
        lock (_gate)
        {
            _players[player.Character.Id] = player;
        }
    }

    public FieldPlayer? Leave(int characterId)
    {
        lock (_gate)
        {
            if (_players.Remove(characterId, out FieldPlayer? player))
            {
                return player;
            }

            return null;
        }
    }

    /// <summary>Sends <paramref name="packet"/> to everyone except <paramref name="exceptCharacterId"/>.</summary>
    public async ValueTask BroadcastAsync(byte[] packet, int exceptCharacterId = -1)
    {
        foreach (FieldPlayer player in Players)
        {
            if (player.Character.Id == exceptCharacterId)
            {
                continue;
            }

            try
            {
                await player.Session.SendAsync(packet).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A dead session drops out on its own disconnect path; keep broadcasting.
            }
        }
    }
}

/// <summary>Fields by map id, created on demand.</summary>
public sealed class FieldRegistry
{
    private readonly Dictionary<int, Field> _fields = new();
    private readonly object _gate = new();

    public Field Get(int mapId)
    {
        lock (_gate)
        {
            if (!_fields.TryGetValue(mapId, out Field? field))
            {
                field = new Field(mapId);
                _fields[mapId] = field;
            }

            return field;
        }
    }
}
