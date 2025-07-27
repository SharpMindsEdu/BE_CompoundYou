using Application.Features.Users.Queries;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Users.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public class SearchUserByNameQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact(Skip = "Require Change")]
    public async Task SearchUserByName_WithMatchingName_ShouldReturnUsers()
    {
        // Arrange
        var user1 = new User { DisplayName = "Alice Example" };
        var user2 = new User { DisplayName = "Bob Example" };
        PersistWithDatabase(db => db.AddRange(user1, user2));

        var query = new SearchUsersByName.SearchUserByNameQuery("Example");

        // Act
        var result = await Send(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data);
        Assert.All(result.Data, u => Assert.Contains("Example", u.DisplayName));
    }

    [Fact]
    public async Task SearchUserByName_WithEmptyName_ShouldThrowValidationException()
    {
        // Arrange
        var query = new SearchUsersByName.SearchUserByNameQuery("");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(query, TestContext.Current.CancellationToken)
        );

        Assert.Contains("Term", ex.Message);
    }

    [Fact]
    public async Task SearchUserByName_WithNoMatchingUsers_ShouldReturnEmptyList()
    {
        // Arrange
        var query = new SearchUsersByName.SearchUserByNameQuery("nonexistent-name");

        // Act
        var result = await Send(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task SearchUserByName_WithMatchingEmail_ShouldReturnUser()
    {
        var user = new User { DisplayName = "EmailUser", Email = "mail@test.com" };
        PersistWithDatabase(db => db.Add(user));

        var query = new SearchUsersByName.SearchUserByNameQuery("mail@test.com");
        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Single(result.Data);
        Assert.Equal(user.Email, result.Data[0].Email);
    }

    [Fact]
    public async Task SearchUserByName_WithMatchingPhone_ShouldReturnUser()
    {
        var user = new User { DisplayName = "PhoneUser", PhoneNumber = "+499999" };
        PersistWithDatabase(db => db.Add(user));

        var query = new SearchUsersByName.SearchUserByNameQuery("+499999");
        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Single(result.Data);
        Assert.Equal(user.DisplayName, result.Data[0].DisplayName);
    }
}
