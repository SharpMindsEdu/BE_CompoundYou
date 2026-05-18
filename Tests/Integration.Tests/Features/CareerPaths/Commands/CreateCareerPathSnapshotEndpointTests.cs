using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Application.Features.CareerPaths.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerPaths.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerPathTests)]
public sealed class CreateCareerPathSnapshotEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateCareerPathSnapshot_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(CreateCareerPathSnapshot.Endpoint, ("employeeId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CreateCareerPathSnapshot_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var manager = await CreateManagerWithReportAsync(ct);
        var report = await GetFirstDirectReportAsync(manager, ct);
        var career = await SeedCareerPathDataAsync(manager.Tenant, report, manager.Employee, ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            Route("api/career-paths/employees/{employeeId:long}/snapshots", ("employeeId", report.Id)),
            manager.Token,
            new { EmployeeId = report.Id, TargetRoleProfileId = career.TargetRole.Id },
            ct
        );

        Assert.Equal(report.Id, GetRequiredLong(json, "employeeId"));

        await using var db = CreateDbContext(manager.Tenant.Id);
        Assert.True(await db.Set<CareerPathSnapshot>().AnyAsync(x => x.EmployeeId == report.Id, ct));
    
    }
}
