using Application.Features.Departments.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Departments.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.DepartmentTests)]
public sealed class UpdateDepartmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateDepartment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateDepartment.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
