using Application.Features.Employees.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class GetEmployeeEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetEmployee_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetEmployee.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
