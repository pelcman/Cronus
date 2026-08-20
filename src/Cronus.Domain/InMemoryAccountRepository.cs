using System.Collections.Concurrent;

namespace Cronus.Domain;

/// <summary>
/// Thread-safe in-memory account store for local development and tests. Not persistent.
/// </summary>
public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<string, Account> _accounts =
        new(StringComparer.OrdinalIgnoreCase);
    private int _nextId;

    public Account? Find(string loginId)
        => _accounts.TryGetValue(loginId, out Account? account) ? account : null;

    public Account Create(string loginId, string password, byte gender)
    {
        var account = new Account
        {
            Id = Interlocked.Increment(ref _nextId),
            LoginId = loginId,
            Password = password,
            Gender = gender,
        };

        return _accounts.GetOrAdd(loginId, account);
    }
}
