using Application.Features.Employees.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class DeactivateEmployeeEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task DeactivateEmployee_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(DeactivateEmployee.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
