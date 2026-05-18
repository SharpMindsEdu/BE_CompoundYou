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
}
