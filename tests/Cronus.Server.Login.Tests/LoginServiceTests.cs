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
}
