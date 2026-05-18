using Application.Features.CareerPaths.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerPaths.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerPathTests)]
public sealed class CreateCareerPathSnapshotEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateCareerPathSnapshot_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(CreateCareerPathSnapshot.Endpoint, ("employeeId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
