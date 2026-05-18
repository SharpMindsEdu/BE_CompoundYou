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
}
