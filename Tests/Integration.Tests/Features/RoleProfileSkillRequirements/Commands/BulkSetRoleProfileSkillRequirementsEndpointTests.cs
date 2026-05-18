using Application.Features.RoleProfileSkillRequirements.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfileSkillRequirements.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileSkillRequirementTests)]
public sealed class BulkSetRoleProfileSkillRequirementsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task BulkSetRoleProfileSkillRequirements_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(BulkSetRoleProfileSkillRequirements.Endpoint, ("roleProfileId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
