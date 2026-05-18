using Application.Features.Departments.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Departments.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.DepartmentTests)]
public sealed class DeleteDepartmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task DeleteDepartment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Delete,
            Route(DeleteDepartment.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
