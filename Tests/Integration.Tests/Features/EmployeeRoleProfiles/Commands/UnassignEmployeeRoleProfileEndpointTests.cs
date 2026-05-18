using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Application.Features.EmployeeRoleProfiles.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeRoleProfiles.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeRoleProfileTests)]
public sealed class UnassignEmployeeRoleProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UnassignEmployeeRoleProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Delete,
            Route(UnassignEmployeeRoleProfile.Endpoint, ("employeeId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task UnassignEmployeeRoleProfile_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var employee = await SeedEmployeeAsync(ctx.Tenant, cancellationToken: ct);
        var role = await SeedRoleProfileAsync(ctx.Tenant, cancellationToken: ct);
        await SeedEmployeeRoleProfileAsync(ctx.Tenant, employee, role, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Delete,
            Route("api/employees/{employeeId:long}/role-profile", ("employeeId", employee.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.True(json.GetBoolean());
        await using var db = CreateDbContext(ctx.Tenant.Id);
        Assert.False(
            await db.Set<EmployeeRoleProfile>()
                .AnyAsync(x => x.EmployeeId == employee.Id && x.IsActive, ct)
        );
    
    }
}
