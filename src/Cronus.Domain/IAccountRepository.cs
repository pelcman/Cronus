namespace Cronus.Domain;

/// <summary>
/// Account store port. The login layer depends on this interface; infrastructure adapters
/// (in-memory for dev/tests, EF Core for MySQL) implement it.
/// </summary>
public interface IAccountRepository
{
    Account? Find(string loginId);

    Account Create(string loginId, string password, byte gender);
}
