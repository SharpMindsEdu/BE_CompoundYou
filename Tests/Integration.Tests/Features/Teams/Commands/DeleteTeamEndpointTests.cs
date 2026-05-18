using Application.Features.Teams.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Teams.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class DeleteTeamEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task DeleteTeam_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Delete,
            Route(DeleteTeam.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
