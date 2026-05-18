using Domain.Enums;
using Application.Features.TenantMemberships.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.TenantMemberships.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantMembershipTests)]
public sealed class AcceptInviteEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task AcceptInvite_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            AcceptInvite.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task AcceptInvite_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var tenant = await SeedTenantAsync(cancellationToken: ct);
        var invitation = await SeedTenantInvitationAsync(
            tenant,
            email: ctx.User.Email,
            role: TenantRole.Employee,
            cancellationToken: ct
        );

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/tenants/invitations/accept",
            ctx.Token,
            new { Token = invitation.Token },
            ct
        );

        Assert.Equal(tenant.Id, GetRequiredLong(json, "tenantId"));
        Assert.Equal(ctx.User.Id, GetRequiredLong(json, "userId"));
    
    }
}
