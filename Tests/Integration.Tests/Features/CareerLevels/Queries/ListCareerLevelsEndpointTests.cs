using Application.Features.CareerLevels.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerLevels.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerLevelTests)]
public sealed class ListCareerLevelsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListCareerLevels_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(ListCareerLevels.Endpoint, ("jobFamilyId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
