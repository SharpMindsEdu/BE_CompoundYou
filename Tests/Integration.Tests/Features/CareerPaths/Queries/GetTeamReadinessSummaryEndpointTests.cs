using Domain.Enums;
using Application.Features.CareerPaths.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerPaths.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerPathTests)]
public sealed class GetTeamReadinessSummaryEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetTeamReadinessSummary_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetTeamReadinessSummary.Endpoint, ("teamId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetTeamReadinessSummary_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var manager = await CreateTenantContextAsync(TenantRole.Manager, cancellationToken: ct);
        Assert.NotNull(manager.Employee);
        var team = await SeedTeamAsync(
            manager.Tenant,
            manager: manager.Employee,
            cancellationToken: ct
        );
        var report = await SeedEmployeeAsync(
            manager.Tenant,
            team: team,
            manager: manager.Employee,
            cancellationToken: ct
        );
        await SeedCareerPathDataAsync(manager.Tenant, report, manager.Employee, ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/career-paths/teams/{teamId:long}/readiness", ("teamId", team.Id)),
            manager.Token,
            cancellationToken: ct
        );

        Assert.Equal(team.Id, GetRequiredLong(json, "teamId"));
        AssertArrayContainsLongProperty(json.GetProperty("employees"), "employeeId", report.Id);
    
    }
}
