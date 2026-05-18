using Domain.Entities;
using Domain.Enums;
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

    [Fact]
    public async Task CreateDepartment_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var name = UniqueName("Department");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/departments",
            ctx.Token,
            new { Name = name, ParentDepartmentId = (long?)null },
            ct
        );

        Assert.Equal(name, GetRequiredString(json, "name"));
        await AssertEntityExistsAsync<Department>(GetRequiredLong(json, "id"), ctx.Tenant, ct);
    
    }
}
