using Application.Features.Employees.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class ListEmployeesEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListEmployees_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListEmployees.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ListEmployees_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var manager = await CreateManagerWithReportAsync(ct);
        Assert.NotNull(manager.Employee);
        var report = await GetFirstDirectReportAsync(manager, ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            WithQuery("api/employees", ("managerEmployeeId", manager.Employee.Id)),
            manager.Token,
            cancellationToken: ct
        );

        AssertPageContainsId(json, report.Id);
    
    }
}
