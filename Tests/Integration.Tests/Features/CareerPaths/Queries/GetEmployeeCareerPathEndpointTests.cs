using Application.Features.CareerPaths.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerPaths.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerPathTests)]
public sealed class GetEmployeeCareerPathEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetEmployeeCareerPath_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetEmployeeCareerPath.Endpoint, ("employeeId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
