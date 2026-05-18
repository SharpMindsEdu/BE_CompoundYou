using Domain.Enums;
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

    [Fact]
    public async Task GetMyTenants_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/tenants/me",
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, ctx.Tenant.Id);
    
    }
}
