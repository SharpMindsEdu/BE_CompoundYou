using Application.Features.Tenants.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Tenants.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class SuspendTenantEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SuspendTenant_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(SuspendTenant.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
