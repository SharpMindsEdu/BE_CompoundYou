using Application.Features.Departments.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Departments.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.DepartmentTests)]
public sealed class ListDepartmentsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListDepartments_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListDepartments.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
