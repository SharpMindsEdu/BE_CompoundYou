using Domain.Enums;
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

    [Fact]
    public async Task UpdateTenant_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var name = UniqueName("Updated Tenant");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/tenants/{id:long}", ("id", ctx.Tenant.Id)),
            ctx.Token,
            new { Id = ctx.Tenant.Id, Name = name, Plan = "enterprise" },
            ct
        );

        Assert.Equal(ctx.Tenant.Id, GetRequiredLong(json, "id"));
        Assert.Equal(name, GetRequiredString(json, "name"));
        Assert.Equal("enterprise", GetRequiredString(json, "plan"));
    
    }
}
