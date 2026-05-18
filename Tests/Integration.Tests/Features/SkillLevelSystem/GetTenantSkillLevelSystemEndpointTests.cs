using Application.Features.SkillLevelSystem;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.SkillLevelSystem;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillLevelSystemTests)]
public sealed class GetTenantSkillLevelSystemEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetTenantSkillLevelSystem_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            TenantSkillLevelSystem.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
