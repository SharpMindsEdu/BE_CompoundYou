using Application.Features.Teams.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Teams.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class UpdateTeamEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateTeam_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateTeam.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
