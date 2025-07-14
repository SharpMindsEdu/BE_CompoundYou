using Application.Features.Users.Queries;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Users.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public class GetUserByIdQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetUser_WithValidId_ShouldReturnUserDto()
    {
        // Arrange
        var user = new User { DisplayName = "Jane Doe", Email = "jane@example.com" };
        PersistWithDatabase(db => db.Add(user));

        var query = new GetUser.GetUserQuery(user.Id);

        // Act
        var result = await Send(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.DisplayName, result.DisplayName);
        Assert.Equal(user.Email, result.Email);
    }

    [Fact]
    public async Task GetUser_WithNonExistingId_ShouldReturnNullDto()
    {
        // Arrange
        var query = new GetUser.GetUserQuery(15);

        // Act
        var result = await Send(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result); // Du kannst hier alternativ throw oder NotFound-Logik testen, falls erwünscht
    }
}
