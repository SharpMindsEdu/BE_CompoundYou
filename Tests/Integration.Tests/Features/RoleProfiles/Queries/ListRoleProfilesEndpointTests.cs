using Application.Features.RoleProfiles.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfiles.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileTests)]
public sealed class ListRoleProfilesEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListRoleProfiles_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListRoleProfiles.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
