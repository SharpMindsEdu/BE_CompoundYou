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
}
