namespace Cronus.Server.Login;

/// <summary>A channel within a world.</summary>
public sealed class GameChannel
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Approximate online population; scaled by ×200 on the wire (nUserNo).</summary>
    public int OnlineCount { get; set; }

    /// <summary>Adult/language flag byte sent per channel.</summary>
    public byte Language { get; init; }
}

/// <summary>A world (server group) and its channels.</summary>
public sealed class GameWorld
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public string EventDescription { get; init; } = string.Empty;

    public required IReadOnlyList<GameChannel> Channels { get; init; }
}
