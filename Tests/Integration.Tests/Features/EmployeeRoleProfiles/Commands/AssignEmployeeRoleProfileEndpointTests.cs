using Domain.Enums;
using Application.Features.EmployeeRoleProfiles.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeRoleProfiles.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeRoleProfileTests)]
public sealed class AssignEmployeeRoleProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task AssignEmployeeRoleProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(AssignEmployeeRoleProfile.Endpoint, ("employeeId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task AssignEmployeeRoleProfile_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var employee = await SeedEmployeeAsync(ctx.Tenant, cancellationToken: ct);
        var role = await SeedRoleProfileAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            Route("api/employees/{employeeId:long}/role-profile", ("employeeId", employee.Id)),
            ctx.Token,
            new { EmployeeId = employee.Id, RoleProfileId = role.Id },
            ct
        );

        Assert.Equal(employee.Id, GetRequiredLong(json, "employeeId"));
        Assert.Equal(role.Id, GetRequiredLong(json, "roleProfileId"));
    
    }
}
