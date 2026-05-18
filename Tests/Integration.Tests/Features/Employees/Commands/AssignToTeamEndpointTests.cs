using Application.Features.Employees.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class AssignToTeamEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task AssignToTeam_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(AssignToTeam.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
