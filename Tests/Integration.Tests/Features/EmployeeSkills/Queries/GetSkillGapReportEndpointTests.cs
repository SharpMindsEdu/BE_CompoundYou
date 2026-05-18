using Application.Features.EmployeeSkills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class GetSkillGapReportEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetSkillGapReport_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetSkillGapReport.Endpoint, ("employeeId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
