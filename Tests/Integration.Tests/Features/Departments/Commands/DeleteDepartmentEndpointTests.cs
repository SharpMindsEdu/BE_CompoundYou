using Domain.Entities;
using Domain.Enums;
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

    [Fact]
    public async Task DeleteDepartment_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var department = await SeedDepartmentAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Delete,
            Route("api/departments/{id:long}", ("id", department.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.True(json.GetBoolean());
        await using var db = CreateDbContext(ctx.Tenant.Id);
        Assert.Null(await db.Set<Department>().FindAsync([department.Id], ct));
    
    }
}
