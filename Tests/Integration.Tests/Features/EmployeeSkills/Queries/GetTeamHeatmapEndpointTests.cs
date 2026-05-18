using Domain.Enums;
using Application.Features.EmployeeSkills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class GetTeamHeatmapEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetTeamHeatmap_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetTeamHeatmap.Endpoint, ("teamId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetTeamHeatmap_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var manager = await CreateTenantContextAsync(TenantRole.Manager, cancellationToken: ct);
        Assert.NotNull(manager.Employee);
        var team = await SeedTeamAsync(manager.Tenant, manager: manager.Employee, cancellationToken: ct);
        var employee = await SeedEmployeeAsync(
            manager.Tenant,
            team: team,
            manager: manager.Employee,
            cancellationToken: ct
        );
        var skill = await SeedSkillAsync(manager.Tenant, cancellationToken: ct);
        var level = await SeedSkillLevelAsync(manager.Tenant, cancellationToken: ct);
        await SeedEmployeeSkillAssessmentAsync(
            manager.Tenant,
            employee,
            skill,
            level,
            validatedSkillLevel: level,
            status: SkillAssessmentStatus.Validated,
            cancellationToken: ct
        );

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/employee-skills/teams/{teamId:long}/heatmap", ("teamId", team.Id)),
            manager.Token,
            cancellationToken: ct
        );

        Assert.Equal(team.Id, GetRequiredLong(json, "teamId"));
        AssertArrayContainsLongProperty(json.GetProperty("employees"), "employeeId", employee.Id);
    
    }
}
