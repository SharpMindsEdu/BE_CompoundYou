using Application.Features.Skills.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Skills.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class UpdateSkillEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateSkill_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateSkill.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
