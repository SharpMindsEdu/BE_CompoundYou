using Application.Features.TeamSkillRequirements.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.TeamSkillRequirements.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamSkillRequirementTests)]
public sealed class ListTeamSkillRequirementsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListTeamSkillRequirements_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(ListTeamSkillRequirements.Endpoint, ("teamId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
