using Application.Features.Skills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Skills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class GetSkillTreeEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetSkillTree_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetSkillTree.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
