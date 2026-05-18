using Application.Features.Skills.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Skills.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class CreateSkillEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateSkill_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateSkill.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
