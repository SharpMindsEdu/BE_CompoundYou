using Application.Features.Users.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Users.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class GetUserEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetUser_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetUser.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetUser_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/users",
            ctx.Token,
            cancellationToken: ct
        );

        Assert.Equal(ctx.User.Id, GetRequiredLong(json, "id"));
        Assert.Equal(ctx.User.Email, GetRequiredString(json, "email"));
    
    }
}
