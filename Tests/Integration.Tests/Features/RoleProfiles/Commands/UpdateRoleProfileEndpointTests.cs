using Application.Features.RoleProfiles.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfiles.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileTests)]
public sealed class UpdateRoleProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateRoleProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateRoleProfile.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
