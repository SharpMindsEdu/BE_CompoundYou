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
}
