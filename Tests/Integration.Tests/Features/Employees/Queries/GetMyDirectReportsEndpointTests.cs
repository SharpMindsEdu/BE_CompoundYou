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
}
