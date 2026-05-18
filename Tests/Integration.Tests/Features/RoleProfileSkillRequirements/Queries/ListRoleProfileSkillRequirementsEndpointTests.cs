using Application.Features.RoleProfileSkillRequirements.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfileSkillRequirements.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileSkillRequirementTests)]
public sealed class ListRoleProfileSkillRequirementsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListRoleProfileSkillRequirements_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(ListRoleProfileSkillRequirements.Endpoint, ("roleProfileId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
