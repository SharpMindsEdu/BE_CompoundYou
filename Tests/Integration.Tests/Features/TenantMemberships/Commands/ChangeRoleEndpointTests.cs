using Domain.Enums;
using Application.Features.TenantMemberships.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.TenantMemberships.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantMembershipTests)]
public sealed class ChangeRoleEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ChangeRole_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(ChangeRole.Endpoint, ("tenantId", 1), ("membershipId", 2)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ChangeRole_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var target = await SeedUserAsync(cancellationToken: ct);
        var membership = await SeedTenantMembershipAsync(ctx.Tenant, target, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route(
                "api/tenants/{tenantId:long}/memberships/{membershipId:long}/role",
                ("tenantId", ctx.Tenant.Id),
                ("membershipId", membership.Id)
            ),
            ctx.Token,
            new { Role = TenantRole.Manager },
            ct
        );

        Assert.Equal((int)TenantRole.Manager, json.GetProperty("role").GetInt32());
    
    }
}
