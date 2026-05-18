using Application.Features.Tenants.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Tenants.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class CreateTenantEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateTenant_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateTenant.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
