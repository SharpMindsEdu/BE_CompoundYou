using Application.Features.Employees.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class UpdateEmployeeEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateEmployee_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateEmployee.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
