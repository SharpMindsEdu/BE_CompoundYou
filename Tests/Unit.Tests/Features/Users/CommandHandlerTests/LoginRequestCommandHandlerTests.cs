using Application.Features.Users.Commands;
using Application.Shared;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Users.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public class RequestLoginCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task RequestLogin_WithValidEmail_ShouldSetNewCode()
    {
        var user = new User
        {
            Email = "user@example.com",
            SignInSecret = "0",
            SignInTries = 1,
            DisplayName = "TestUser",
        };

        PersistWithDatabase(db => db.Add(user));

        var command = new RequestLogin.RequestLoginCommand(user.Email, null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Data);

        WithDatabase(db =>
        {
            var updated = db.Set<User>().First(x => x.Id == user.Id);
            Assert.NotNull(updated.SignInSecret);
            Assert.Equal(3, updated.SignInTries);
            Assert.Matches(@"^\d{6}$", updated.SignInSecret!);
            //Assert.NotEqual("123456", updated.SignInSecret);
        });
    }

    [Fact]
    public async Task RequestLogin_WithMissingUser_ShouldReturnNotFound()
    {
        var command = new RequestLogin.RequestLoginCommand("notfound@example.com", null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(ErrorResults.EntityNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task RequestLogin_WithNoEmailOrPhone_ShouldThrowValidationException()
    {
        var command = new RequestLogin.RequestLoginCommand(null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains(ValidationErrors.EmailAndPhoneNumberMissing, ex.Message);
    }
}
