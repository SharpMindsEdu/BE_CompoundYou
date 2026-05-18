using Application.Features.Employees.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class CreateEmployeeEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateEmployee_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateEmployee.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
