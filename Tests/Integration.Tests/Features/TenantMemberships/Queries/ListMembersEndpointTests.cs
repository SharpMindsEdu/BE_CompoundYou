using Domain.Enums;
using Application.Features.TenantMemberships.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.TenantMemberships.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantMembershipTests)]
public sealed class ListMembersEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListMembers_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(ListMembers.Endpoint, ("tenantId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ListMembers_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var target = await SeedUserAsync(cancellationToken: ct);
        var membership = await SeedTenantMembershipAsync(ctx.Tenant, target, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/tenants/{tenantId:long}/memberships", ("tenantId", ctx.Tenant.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        AssertPageContainsId(json, membership.Id);
    
    }
}
