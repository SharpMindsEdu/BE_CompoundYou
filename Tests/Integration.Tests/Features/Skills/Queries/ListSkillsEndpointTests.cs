using Application.Features.Skills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Skills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class ListSkillsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListSkills_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListSkills.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
