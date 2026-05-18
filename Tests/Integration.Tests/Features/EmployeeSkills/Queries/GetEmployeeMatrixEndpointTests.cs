using Domain.Enums;
using Application.Features.EmployeeSkills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class GetEmployeeMatrixEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetEmployeeMatrix_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetEmployeeMatrix.Endpoint, ("employeeId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetEmployeeMatrix_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var seed = await SeedManagerAssessmentAsync(SkillAssessmentStatus.Validated, ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/employee-skills/matrix/{employeeId:long}", ("employeeId", seed.Employee.Id)),
            seed.Manager.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, seed.Assessment.Id);
    
    }
}
