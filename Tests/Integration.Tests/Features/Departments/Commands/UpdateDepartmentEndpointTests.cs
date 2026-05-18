using Domain.Enums;
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

    [Fact]
    public async Task UpdateDepartment_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var department = await SeedDepartmentAsync(ctx.Tenant, cancellationToken: ct);
        var name = UniqueName("Updated Department");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/departments/{id:long}", ("id", department.Id)),
            ctx.Token,
            new { Id = department.Id, Name = name, ParentDepartmentId = (long?)null },
            ct
        );

        Assert.Equal(department.Id, GetRequiredLong(json, "id"));
        Assert.Equal(name, GetRequiredString(json, "name"));
    
    }
}
