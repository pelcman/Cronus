using Cronus.Domain;

namespace Cronus.Server.Channel;

/// <summary>
/// A server tick that periodically persists every online character, so an unexpected shutdown loses
/// at most one interval's progress. Most stat changes already save on mutation (exp, meso, level,
/// AP, map); this is the safety net that also flushes the drift that isn't saved eagerly (notably
/// regen'd HP/MP). Timed world logic on a tick, decoupled from client packets (see CLAUDE.md §2).
/// </summary>
public sealed class CharacterAutoSaveService
{
    private readonly FieldRegistry _fields;
    private readonly ICharacterRepository _characters;
    private readonly TimeSpan _interval;

    public CharacterAutoSaveService(FieldRegistry fields, ICharacterRepository characters, TimeSpan? interval = null)
    {
        _fields = fields;
        _characters = characters;
        _interval = interval ?? TimeSpan.FromMinutes(2);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Tick();
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// <summary>Persists every online character once and returns how many were saved (for tests).</summary>
    public int Tick()
    {
        var seen = new HashSet<int>();
        int saved = 0;
        foreach (Field field in _fields.Fields)
        {
            foreach (FieldPlayer player in field.Players)
            {
                if (!seen.Add(player.Character.Id))
                {
                    continue; // avoid a double-save if a character is briefly in two fields
                }

                try
                {
                    _characters.Save(player.Character);
                    saved++;
                }
                catch (Exception)
                {
                    // one failing save shouldn't stop the sweep; it'll retry next tick
                }
            }
        }

        return saved;
    }
}
