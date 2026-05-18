using Application.Features.SkillCategories.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.SkillCategories.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillCategoryTests)]
public sealed class UpdateSkillCategoryEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateSkillCategory_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateSkillCategory.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
