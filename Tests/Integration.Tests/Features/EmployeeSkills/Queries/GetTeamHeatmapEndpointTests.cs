using Application.Features.EmployeeSkills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class GetTeamHeatmapEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetTeamHeatmap_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetTeamHeatmap.Endpoint, ("teamId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
