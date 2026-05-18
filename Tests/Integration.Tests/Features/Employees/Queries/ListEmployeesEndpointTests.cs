using Application.Features.Employees.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class ListEmployeesEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListEmployees_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListEmployees.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
