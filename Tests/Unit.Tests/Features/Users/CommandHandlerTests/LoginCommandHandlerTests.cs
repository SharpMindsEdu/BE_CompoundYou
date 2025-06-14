using Application.Common;
using Application.Features.Users.Commands;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Users.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public class LoginCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    private const string ValidCode = "123456";

    [Fact]
    public async Task Login_WithValidEmailAndCorrectCode_ShouldSucceed()
    {
        var user = new User
        {
            Email = "test@example.com",
            SignInSecret = ValidCode,
            SignInTries = 3,
            DisplayName = "TestUser",
        };

        PersistWithDatabase(db => db.Add(user));

        var command = new Login.LoginCommand(ValidCode, user.Email, null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
    }

    [Fact]
    public async Task Login_WithWrongCode_DecrementsTries()
    {
        var user = new User
        {
            Email = "test@example.com",
            SignInSecret = ValidCode,
            SignInTries = 3,
            DisplayName = "TestUser",
        };

        PersistWithDatabase(db => db.Add(user));

        var command = new Login.LoginCommand("000000", user.Email, null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("2", result.ErrorMessage);
    }

    [Fact]
    public async Task Login_WithWrongCodeAndNoTriesLeft_ShouldResetSecret()
    {
        var user = new User
        {
            Email = "locked@example.com",
            SignInSecret = ValidCode,
            SignInTries = 1,
            DisplayName = "TestUser",
        };

        PersistWithDatabase(db => db.Add(user));

        var command = new Login.LoginCommand("000000", user.Email, null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorResults.SignInFailed, result.ErrorMessage);
    }

    [Fact]
    public async Task Login_WithNoMatchingUser_ShouldReturnNotFound()
    {
        var command = new Login.LoginCommand(ValidCode, "missing@example.com", null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(ErrorResults.EntityNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task Login_WithMissingSecret_ShouldReturnSignInNotFound()
    {
        var user = new User
        {
            Email = "no-secret@example.com",
            SignInSecret = null,
            SignInTries = 3,
            DisplayName = "TestUser",
        };

        PersistWithDatabase(db => db.Add(user));

        var command = new Login.LoginCommand(ValidCode, user.Email, null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorResults.SignInNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task Login_WithPhoneNumberAndCorrectCode_ShouldSucceed()
    {
        var user = new User
        {
            PhoneNumber = "+49123456789",
            SignInSecret = ValidCode,
            SignInTries = 3,
            DisplayName = "PhoneUser",
        };

        PersistWithDatabase(db => db.Add(user));

        var command = new Login.LoginCommand(ValidCode, null, user.PhoneNumber);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
    }

    [Fact]
    public async Task Login_WithBothEmailAndPhone_PrefersEmail()
    {
        var user = new User
        {
            Email = "primary@example.com",
            PhoneNumber = "+4911111111",
            SignInSecret = ValidCode,
            SignInTries = 3,
            DisplayName = "DualInputUser",
        };

        PersistWithDatabase(db => db.Add(user));

        var command = new Login.LoginCommand(ValidCode, user.Email, "ignored@domain.com");

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Login_WithMissingEmailAndPhone_ShouldThrowValidationException()
    {
        var command = new Login.LoginCommand(ValidCode, null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains(ValidationErrors.EmailAndPhoneNumberMissing, ex.Message);
    }

    [Fact]
    public async Task Login_WithEmptyCode_ShouldThrowValidationException()
    {
        var command = new Login.LoginCommand("", "user@example.com", null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Code", ex.Message);
    }

    [Fact]
    public async Task Login_WithCodeTooShort_ShouldThrowValidationException()
    {
        var command = new Login.LoginCommand("123", "user@example.com", null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Code", ex.Message);
    }
}
