using Cronus.Domain;
using Cronus.Server.Login;
using Xunit;

namespace Cronus.Server.Login.Tests;

public class LoginServiceTests
{
    [Fact]
    public void AutoRegistersUnknownAccount()
    {
        var service = new LoginService(new InMemoryAccountRepository());

        LoginService.Outcome outcome = service.Authenticate("player01", "secret");

        Assert.Equal(LoginResult.Success, outcome.Result);
        Assert.NotNull(outcome.Account);
        Assert.Equal("player01", outcome.Account!.LoginId);
    }

    [Fact]
    public void SecondLoginWithSamePasswordSucceeds()
    {
        var repo = new InMemoryAccountRepository();
        var service = new LoginService(repo);

        int firstId = service.Authenticate("player01", "secret").Account!.Id;
        LoginService.Outcome second = service.Authenticate("player01", "secret");

        Assert.Equal(LoginResult.Success, second.Result);
        Assert.Equal(firstId, second.Account!.Id);
    }

    [Fact]
    public void WrongPasswordIsRejected()
    {
        var service = new LoginService(new InMemoryAccountRepository());
        service.Authenticate("player01", "secret");

        LoginService.Outcome outcome = service.Authenticate("player01", "wrong");

        Assert.Equal(LoginResult.IncorrectPassword, outcome.Result);
        Assert.Null(outcome.Account);
    }

    [Fact]
    public void TrailingUnderscoreSelectsFemaleGender()
    {
        var service = new LoginService(new InMemoryAccountRepository());

        LoginService.Outcome outcome = service.Authenticate("player_", "secret");

        Assert.Equal(LoginResult.Success, outcome.Result);
        Assert.Equal("player", outcome.Account!.LoginId);
        Assert.Equal(1, outcome.Account.Gender);
    }

    [Fact]
    public void AutoRegisterDisabledRejectsUnknown()
    {
        var service = new LoginService(new InMemoryAccountRepository(), autoRegister: false);

        LoginService.Outcome outcome = service.Authenticate("nobody", "secret");

        Assert.Equal(LoginResult.NotRegistered, outcome.Result);
    }

    [Fact]
    public void NewAccountStoresABcryptHash_NotThePlaintext()
    {
        var repo = new InMemoryAccountRepository();
        var service = new LoginService(repo);

        service.Authenticate("player01", "secret");

        Account stored = repo.Find("player01")!;
        Assert.NotEqual("secret", stored.Password);
        Assert.StartsWith("$2", stored.Password);
        Assert.True(PasswordHasher.Verify("secret", stored.Password));
    }

    [Fact]
    public void LegacyPlaintextAccount_LogsInAndIsUpgradedToAHash()
    {
        var repo = new InMemoryAccountRepository();
        repo.Create("oldtimer", "plainpw", gender: 0); // a pre-hashing row

        var service = new LoginService(repo);
        LoginService.Outcome outcome = service.Authenticate("oldtimer", "plainpw");

        Assert.Equal(LoginResult.Success, outcome.Result);
        Account stored = repo.Find("oldtimer")!;
        Assert.StartsWith("$2", stored.Password);                 // upgraded in place
        Assert.True(PasswordHasher.Verify("plainpw", stored.Password));

        // And the hashed row still authenticates / still rejects a wrong password.
        Assert.Equal(LoginResult.Success, service.Authenticate("oldtimer", "plainpw").Result);
        Assert.Equal(LoginResult.IncorrectPassword, service.Authenticate("oldtimer", "nope").Result);
    }
}
