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

    [Fact]
    public async Task ListTenants_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var admin = await CreatePlatformAdminContextAsync(ct);
        var tenant = await SeedTenantAsync(cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/tenants",
            admin.Token,
            cancellationToken: ct
        );

        AssertPageContainsId(json, tenant.Id);
    
    }
}
