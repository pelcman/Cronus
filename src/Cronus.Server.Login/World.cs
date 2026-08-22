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

/// <summary>
/// The set of worlds this login server advertises. Interim: an in-memory single world, wired
/// up in <see cref="CreateDefault"/>. Later this comes from config / the World service.
/// </summary>
public sealed class WorldRegistry
{
    public WorldRegistry(IReadOnlyList<GameWorld> worlds) => Worlds = worlds;

    public IReadOnlyList<GameWorld> Worlds { get; }

    public GameWorld? Find(int id)
    {
        foreach (GameWorld world in Worlds)
        {
            if (world.Id == id)
            {
                return world;
            }
        }

        return null;
    }

    /// <summary>One world ("Cronus", id 0) with the given number of channels (default two).</summary>
    public static WorldRegistry CreateDefault(int channelCount = 2)
    {
        var channels = new List<GameChannel>();
        for (int i = 0; i < Math.Max(1, channelCount); i++)
        {
            channels.Add(new GameChannel { Id = i, Name = $"Cronus-{i + 1}", OnlineCount = 0 });
        }

        var world = new GameWorld
        {
            Id = 0,
            Name = "Cronus",
            EventDescription = string.Empty,
            Channels = channels,
        };

        return new WorldRegistry(new[] { world });
    }
}
