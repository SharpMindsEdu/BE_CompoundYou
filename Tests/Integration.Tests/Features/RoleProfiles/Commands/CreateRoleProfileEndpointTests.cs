using Application.Features.RoleProfiles.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfiles.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileTests)]
public sealed class CreateRoleProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateRoleProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateRoleProfile.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
