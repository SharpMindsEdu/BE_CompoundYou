using Application.Features.Teams.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Teams.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class CreateTeamEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateTeam_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateTeam.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
