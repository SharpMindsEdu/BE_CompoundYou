using Application.Features.CareerPaths.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerPaths.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerPathTests)]
public sealed class GetTeamReadinessSummaryEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetTeamReadinessSummary_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetTeamReadinessSummary.Endpoint, ("teamId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
