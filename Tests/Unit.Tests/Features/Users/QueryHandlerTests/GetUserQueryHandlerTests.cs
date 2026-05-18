using Application.Features.Users.Queries;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Users.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class GetUserQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetUser_ShouldReturnRequestedUser()
    {
        var user = SeedUser("Current User", "current@example.com");

        var result = await Send(
            new GetUser.GetUserQuery(user.Id),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(user.Id, result.Id);
        Assert.Equal("Current User", result.DisplayName);
    }
}
