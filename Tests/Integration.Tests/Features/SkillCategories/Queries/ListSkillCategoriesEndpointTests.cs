using Application.Features.SkillCategories.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.SkillCategories.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillCategoryTests)]
public sealed class ListSkillCategoriesEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListSkillCategories_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListSkillCategories.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
