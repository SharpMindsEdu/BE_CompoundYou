using Application.Features.CareerLevels.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerLevels.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerLevelTests)]
public sealed class UpdateCareerLevelEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateCareerLevel_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateCareerLevel.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
