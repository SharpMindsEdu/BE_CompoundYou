using Domain.Enums;
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

    [Fact]
    public async Task SuspendTenant_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var admin = await CreatePlatformAdminContextAsync(ct);
        var tenant = await SeedTenantAsync(cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            WithQuery(Route("api/tenants/{id:long}/suspend", ("id", tenant.Id)), ("suspend", true)),
            admin.Token,
            cancellationToken: ct
        );

        Assert.Equal((int)TenantStatus.Suspended, json.GetProperty("status").GetInt32());
    
    }
}
