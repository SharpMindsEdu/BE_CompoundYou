using Application.Features.Tenants.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Tenants.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class GetMyTenantsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetMyTenants_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetMyTenants.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
