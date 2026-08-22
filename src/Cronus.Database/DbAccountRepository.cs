using Cronus.Domain;

namespace Cronus.Database;

/// <summary>
/// EF Core-backed <see cref="IAccountRepository"/>. Uses a short-lived context per operation
/// (login is low-frequency), created via the supplied factory so the same class works with any
/// provider — MySQL in production, the in-memory provider in tests.
/// </summary>
public sealed class DbAccountRepository : IAccountRepository
{
    private readonly Func<CronusDbContext> _contextFactory;

    public DbAccountRepository(Func<CronusDbContext> contextFactory)
        => _contextFactory = contextFactory;

    public Account? Find(string loginId)
    {
        using CronusDbContext db = _contextFactory();
        return db.Accounts.FirstOrDefault(a => a.LoginId == loginId);
    }

    public Account Create(string loginId, string password, byte gender)
    {
        using CronusDbContext db = _contextFactory();

        // Guard against a race / duplicate: return the existing row if present.
        Account? existing = db.Accounts.FirstOrDefault(a => a.LoginId == loginId);
        if (existing is not null)
        {
            return existing;
        }

        var account = new Account
        {
            LoginId = loginId,
            Password = password,
            Gender = gender,
        };

        db.Accounts.Add(account);
        db.SaveChanges();
        return account;
    }

    public Account? FindById(int accountId)
    {
        using CronusDbContext db = _contextFactory();
        return db.Accounts.FirstOrDefault(a => a.Id == accountId);
    }

    public void Save(Account account)
    {
        using CronusDbContext db = _contextFactory();
        db.Accounts.Update(account);
        db.SaveChanges();
    }
}
