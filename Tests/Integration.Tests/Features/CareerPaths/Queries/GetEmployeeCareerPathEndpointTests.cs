using Application.Features.CareerPaths.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerPaths.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerPathTests)]
public sealed class GetEmployeeCareerPathEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetEmployeeCareerPath_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetEmployeeCareerPath.Endpoint, ("employeeId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetEmployeeCareerPath_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var manager = await CreateManagerWithReportAsync(ct);
        var report = await GetFirstDirectReportAsync(manager, ct);
        var career = await SeedCareerPathDataAsync(manager.Tenant, report, manager.Employee, ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            WithQuery(
                Route("api/career-paths/employees/{employeeId:long}", ("employeeId", report.Id)),
                ("targetRoleProfileId", career.TargetRole.Id)
            ),
            manager.Token,
            cancellationToken: ct
        );

        Assert.Equal(report.Id, GetRequiredLong(json, "employeeId"));
        Assert.NotEmpty(json.GetProperty("skillGaps").EnumerateArray());
    
    }
}
