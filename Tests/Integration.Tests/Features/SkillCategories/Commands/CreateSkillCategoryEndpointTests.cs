using Application.Features.SkillCategories.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.SkillCategories.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillCategoryTests)]
public sealed class CreateSkillCategoryEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateSkillCategory_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateSkillCategory.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
