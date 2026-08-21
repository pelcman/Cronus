using System.Collections.Concurrent;

namespace Cronus.Data;

/// <summary>
/// A reactor template's state machine (from <c>Reactor.wz/{id:0000000}.img</c>): each state node
/// may carry an <c>event/0</c> whose <c>state</c> value is the next state; a state without an
/// event is terminal (the reactor is spent).
/// </summary>
public sealed class ReactorData
{
    /// <summary>state → next state; states absent from the map are terminal.</summary>
    public required IReadOnlyDictionary<int, int> Transitions { get; init; }

    /// <summary>True when <paramref name="state"/> has no outgoing transition (broken/spent).</summary>
    public bool IsTerminal(int state) => !Transitions.ContainsKey(state);

    /// <summary>The state a hit advances to (terminal states return themselves).</summary>
    public int NextState(int state) => Transitions.TryGetValue(state, out int next) ? next : state;

    /// <summary>Parses the state machine from a reactor <c>.img</c> document.</summary>
    public static ReactorData FromWz(WzData reactorImg)
    {
        var transitions = new Dictionary<int, int>();
        foreach (WzData stateNode in reactorImg.Children.Values)
        {
            if (!int.TryParse(stateNode.Name, out int state))
            {
                continue; // info / action nodes
            }

            WzData? ev = stateNode.Child("event")?.Child("0");
            if (ev is not null)
            {
                transitions[state] = ev.GetInt("state", state + 1);
            }
        }

        return new ReactorData { Transitions = transitions };
    }
}

/// <summary>Provides reactor templates by id.</summary>
public interface IReactorProvider
{
    ReactorData? GetReactor(int reactorId);
}

/// <summary>Loads reactor templates from a wz_xml tree: <c>Reactor/{id:0000000}.img.xml</c> (cached).</summary>
public sealed class WzReactorProvider : IReactorProvider
{
    private readonly string _wzRoot;
    private readonly ConcurrentDictionary<int, ReactorData?> _cache = new();

    public WzReactorProvider(string wzRoot) => _wzRoot = wzRoot;

    public ReactorData? GetReactor(int reactorId) => _cache.GetOrAdd(reactorId, Load);

    private ReactorData? Load(int reactorId)
    {
        string path = Path.Combine(_wzRoot, "Reactor", $"{reactorId:0000000}.img.xml");
        return File.Exists(path) ? ReactorData.FromWz(WzData.ParseFile(path)) : null;
    }
}

/// <summary>An in-memory reactor provider for tests / seeded content.</summary>
public sealed class InMemoryReactorProvider : IReactorProvider
{
    private readonly Dictionary<int, ReactorData> _reactors;

    public InMemoryReactorProvider(IReadOnlyDictionary<int, ReactorData> reactors)
        => _reactors = new Dictionary<int, ReactorData>(reactors);

    public ReactorData? GetReactor(int reactorId)
        => _reactors.TryGetValue(reactorId, out ReactorData? r) ? r : null;
}
