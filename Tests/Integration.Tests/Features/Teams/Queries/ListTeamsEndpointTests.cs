using Application.Features.Teams.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Teams.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class ListTeamsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListTeams_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListTeams.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
