using Application.Features.SkillLevelSystem;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.SkillLevelSystem;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillLevelSystemTests)]
public sealed class SetTenantSkillLevelSystemEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SetTenantSkillLevelSystem_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            TenantSkillLevelSystem.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
