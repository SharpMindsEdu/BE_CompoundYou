using Application.Features.Skills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Skills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class SearchSkillsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SearchSkills_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            SearchSkills.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
