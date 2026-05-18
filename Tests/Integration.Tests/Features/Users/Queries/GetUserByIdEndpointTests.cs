using Application.Features.Users.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Users.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class GetUserByIdEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetUserById_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetUserById.Endpoint, ("userId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetUserById_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var target = await SeedUserAsync(displayName: UniqueName("Lookup User"), cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/users/{userId:long}", ("userId", target.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.Equal(target.Id, GetRequiredLong(json, "id"));
        Assert.Equal(target.Email, GetRequiredString(json, "email"));
    
    }
}
