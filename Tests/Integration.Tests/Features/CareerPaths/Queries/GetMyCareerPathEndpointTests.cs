using Application.Features.CareerPaths.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerPaths.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerPathTests)]
public sealed class GetMyCareerPathEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetMyCareerPath_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetMyCareerPath.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
