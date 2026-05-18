using Application.Features.Employees.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class GetMyDirectReportsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetMyDirectReports_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetMyDirectReports.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetMyDirectReports_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var manager = await CreateManagerWithReportAsync(ct);
        var report = await GetFirstDirectReportAsync(manager, ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/employees/me/direct-reports",
            manager.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, report.Id);
    
    }
}
