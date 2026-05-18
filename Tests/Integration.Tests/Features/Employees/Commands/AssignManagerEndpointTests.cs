using Domain.Enums;
using Application.Features.Employees.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class AssignManagerEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task AssignManager_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(AssignManager.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task AssignManager_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        Assert.NotNull(ctx.Employee);
        var target = await SeedEmployeeAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/employees/{id:long}/manager", ("id", target.Id)),
            ctx.Token,
            new { ManagerEmployeeId = ctx.Employee.Id },
            ct
        );

        Assert.Equal(ctx.Employee.Id, GetRequiredLong(json, "managerEmployeeId"));
    
    }
}
