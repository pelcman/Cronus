using Cronus.Database;
using Cronus.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cronus.Database.Tests;

/// <summary>
/// Validates <see cref="DbAccountRepository"/> against the EF Core in-memory provider. This
/// exercises the repository logic and the entity mapping; MySQL-specific behavior still needs a
/// live server (tracked in the backlog).
/// </summary>
public class DbAccountRepositoryTests
{
    private static Func<CronusDbContext> InMemoryFactory()
    {
        // A shared root keeps one logical database across the short-lived contexts.
        string dbName = Guid.NewGuid().ToString();
        DbContextOptions<CronusDbContext> options =
            new DbContextOptionsBuilder<CronusDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        return () => new CronusDbContext(options);
    }

    [Fact]
    public void Create_PersistsAndAssignsId()
    {
        var repo = new DbAccountRepository(InMemoryFactory());

        Account created = repo.Create("player01", "secret", gender: 0);

        Assert.True(created.Id > 0);
        Assert.Equal("player01", created.LoginId);
    }

    [Fact]
    public void Find_ReturnsPersistedAccount()
    {
        Func<CronusDbContext> factory = InMemoryFactory();
        var repo = new DbAccountRepository(factory);
        int id = repo.Create("player01", "secret", gender: 1).Id;

        Account? found = repo.Find("player01");

        Assert.NotNull(found);
        Assert.Equal(id, found!.Id);
        Assert.Equal(1, found.Gender);
    }

    [Fact]
    public void Find_ReturnsNullForUnknownAccount()
    {
        var repo = new DbAccountRepository(InMemoryFactory());
        Assert.Null(repo.Find("nobody"));
    }

    [Fact]
    public void Create_IsIdempotentForSameLoginId()
    {
        var repo = new DbAccountRepository(InMemoryFactory());

        int first = repo.Create("player01", "secret", gender: 0).Id;
        int second = repo.Create("player01", "other", gender: 1).Id;

        Assert.Equal(first, second);
    }

    [Fact]
    public void CharacterRepository_CreateListFindNameExists()
    {
        Func<CronusDbContext> factory = InMemoryFactory();
        var repo = new DbCharacterRepository(factory);

        Character created = repo.Create(new Character
        {
            AccountId = 7,
            WorldId = 0,
            Name = "Kaede",
            Face = 20000,
            Hair = 30000,
        });

        Assert.True(created.Id > 0);
        Assert.True(repo.NameExists("kaede")); // case-insensitive
        Assert.False(repo.NameExists("other"));

        IReadOnlyList<Character> list = repo.ListByAccount(7, 0);
        Assert.Single(list);
        Assert.Equal(created.Id, list[0].Id);
        Assert.Empty(repo.ListByAccount(7, 1));   // other world
        Assert.Empty(repo.ListByAccount(8, 0));   // other account

        Character? found = repo.Find(created.Id);
        Assert.NotNull(found);
        Assert.Equal("Kaede", found!.Name);
    }

    [Fact]
    public void CharacterRepository_Delete()
    {
        var repo = new DbCharacterRepository(InMemoryFactory());
        Character c = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Doomed" });

        Assert.True(repo.Delete(c.Id));
        Assert.Null(repo.Find(c.Id));
        Assert.False(repo.NameExists("doomed"));
        Assert.False(repo.Delete(c.Id)); // already gone
    }

    [Fact]
    public void LoginService_WorksOverDbRepository()
    {
        var repo = new DbAccountRepository(InMemoryFactory());
        var service = new Cronus.Server.Login.LoginService(repo);

        Cronus.Server.Login.LoginService.Outcome first = service.Authenticate("player01", "secret");
        Cronus.Server.Login.LoginService.Outcome again = service.Authenticate("player01", "secret");
        Cronus.Server.Login.LoginService.Outcome wrong = service.Authenticate("player01", "bad");

        Assert.Equal(Cronus.Server.Login.LoginResult.Success, first.Result);
        Assert.Equal(first.Account!.Id, again.Account!.Id);
        Assert.Equal(Cronus.Server.Login.LoginResult.IncorrectPassword, wrong.Result);
    }
}
