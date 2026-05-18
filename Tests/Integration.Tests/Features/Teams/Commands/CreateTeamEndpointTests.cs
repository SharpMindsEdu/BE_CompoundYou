using Domain.Enums;
using Application.Features.Teams.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Teams.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class CreateTeamEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateTeam_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateTeam.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CreateTeam_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var department = await SeedDepartmentAsync(ctx.Tenant, cancellationToken: ct);
        var name = UniqueName("Team");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/teams",
            ctx.Token,
            new { Name = name, DepartmentId = department.Id, ManagerEmployeeId = (long?)null },
            ct
        );

        Assert.Equal(name, GetRequiredString(json, "name"));
        Assert.Equal(department.Id, GetRequiredLong(json, "departmentId"));
    
    }
}
