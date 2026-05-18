using Application.Features.RoleProfiles.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfiles.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileTests)]
public sealed class CopyRoleProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CopyRoleProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(CopyRoleProfile.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
