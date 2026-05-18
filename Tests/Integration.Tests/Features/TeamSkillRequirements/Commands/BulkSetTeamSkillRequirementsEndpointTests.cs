using Application.Features.TeamSkillRequirements.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.TeamSkillRequirements.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamSkillRequirementTests)]
public sealed class BulkSetTeamSkillRequirementsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task BulkSetTeamSkillRequirements_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(BulkSetTeamSkillRequirements.Endpoint, ("teamId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
