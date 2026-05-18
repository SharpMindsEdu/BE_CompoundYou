using Application.Features.Users.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Users.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class SearchUsersByNameEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SearchUsersByName_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            SearchUsersByName.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task SearchUsersByName_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var target = await SeedUserAsync(displayName: UniqueName("Search User"), cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            WithQuery("api/users/search", ("term", target.Email)),
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, target.Id);
    
    }
}
