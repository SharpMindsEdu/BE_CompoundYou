using Domain.Enums;
using Application.Features.TenantMemberships.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.TenantMemberships.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantMembershipTests)]
public sealed class InviteUserEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task InviteUser_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(InviteUser.Endpoint, ("tenantId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task InviteUser_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var email = UniqueEmail("tenant-invite");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            Route("api/tenants/{tenantId:long}/invitations", ("tenantId", ctx.Tenant.Id)),
            ctx.Token,
            new { TenantId = ctx.Tenant.Id, Email = email, Role = TenantRole.Employee },
            ct
        );

        Assert.Equal(ctx.Tenant.Id, GetRequiredLong(json, "tenantId"));
        Assert.Equal(email, GetRequiredString(json, "email"));
    
    }
}
