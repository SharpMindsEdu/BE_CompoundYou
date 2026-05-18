using Domain.Enums;
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

    [Fact]
    public async Task ListDepartments_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var department = await SeedDepartmentAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/departments",
            ctx.Token,
            cancellationToken: ct
        );

        AssertPageContainsId(json, department.Id);
    
    }
}
