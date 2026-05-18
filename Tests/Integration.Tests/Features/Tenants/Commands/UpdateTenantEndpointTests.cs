using Application.Features.Tenants.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Tenants.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class UpdateTenantEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateTenant_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateTenant.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
