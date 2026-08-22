namespace Cronus.Server.Game;

/// <summary>
/// A Mystic Door pair (ports <c>MapleDoor</c>): one door standing where the priest cast it, the
/// other at a free door-portal spot (wz portal type 6) in the map's return town. One instance
/// describes both sides; each side's <see cref="Field"/> holds the same instance.
/// </summary>
public sealed class MysticDoor
{
    /// <summary>Priest Mystic Door (the only pre-BB door skill).</summary>
    public const int SkillMysticDoor = 2311002;

    public required int OwnerId { get; init; }

    public required int SkillId { get; init; }

    public required int FieldMapId { get; init; }

    public required short FieldX { get; init; }

    public required short FieldY { get; init; }

    /// <summary>Portal to spawn at when stepping through toward the field side.</summary>
    public required int FieldPortalId { get; init; }

    public required int TownMapId { get; init; }

    public required short TownX { get; init; }

    public required short TownY { get; init; }

    /// <summary>The town's door-portal (type 6) the town side occupies.</summary>
    public required int TownPortalId { get; init; }

    public required DateTime ExpiresAt { get; init; }

    public bool IsTownSide(int mapId) => mapId == TownMapId;

    public (short X, short Y) PositionIn(int mapId)
        => IsTownSide(mapId) ? (TownX, TownY) : (FieldX, FieldY);

    /// <summary>Where stepping through from <paramref name="fromMapId"/> leads.</summary>
    public int TargetMapFor(int fromMapId) => IsTownSide(fromMapId) ? FieldMapId : TownMapId;

    /// <summary>The spawn portal to use when arriving from <paramref name="fromMapId"/>.</summary>
    public int TargetPortalFor(int fromMapId) => IsTownSide(fromMapId) ? FieldPortalId : TownPortalId;
}
