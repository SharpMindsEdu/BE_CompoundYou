using Application.Features.CareerLevels.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerLevels.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerLevelTests)]
public sealed class CreateCareerLevelEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateCareerLevel_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(CreateCareerLevel.Endpoint, ("jobFamilyId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
