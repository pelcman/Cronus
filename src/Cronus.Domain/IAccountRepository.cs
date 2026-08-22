namespace Cronus.Domain;

/// <summary>
/// Account store port. The login layer depends on this interface; infrastructure adapters
/// (in-memory for dev/tests, EF Core for MySQL) implement it.
/// </summary>
public interface IAccountRepository
{
    Account? Find(string loginId);

    /// <summary>Finds an account by its id (cash shop / points), or null.</summary>
    Account? FindById(int accountId);

    Account Create(string loginId, string password, byte gender);

    /// <summary>Persists changed account state (points, cash locker).</summary>
    void Save(Account account);
}
