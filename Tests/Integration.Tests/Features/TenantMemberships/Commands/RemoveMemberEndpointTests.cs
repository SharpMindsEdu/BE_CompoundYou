using Domain.Entities;
using Domain.Enums;
using Application.Features.TenantMemberships.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.TenantMemberships.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantMembershipTests)]
public sealed class RemoveMemberEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task RemoveMember_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Delete,
            Route(RemoveMember.Endpoint, ("tenantId", 1), ("membershipId", 2)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task RemoveMember_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var target = await SeedUserAsync(cancellationToken: ct);
        var membership = await SeedTenantMembershipAsync(ctx.Tenant, target, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Delete,
            Route(
                "api/tenants/{tenantId:long}/memberships/{membershipId:long}",
                ("tenantId", ctx.Tenant.Id),
                ("membershipId", membership.Id)
            ),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.True(json.GetBoolean());
        await using var db = CreateDbContext();
        var updated = await db.Set<TenantMembership>().FindAsync([membership.Id], ct);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
    
    }
}
