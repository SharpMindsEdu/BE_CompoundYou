using Application.Features.Departments.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Departments.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.DepartmentTests)]
public sealed class CreateDepartmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateDepartment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateDepartment.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
