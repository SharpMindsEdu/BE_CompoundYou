using Application.Features.Tenants.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Tenants.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class ListTenantsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListTenants_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListTenants.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
