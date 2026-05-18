using Domain.Enums;
using Application.Features.EmployeeSkills.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class RejectAssessmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task RejectAssessment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(RejectAssessment.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task RejectAssessment_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var seed = await SeedManagerAssessmentAsync(SkillAssessmentStatus.PendingValidation, ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            Route("api/employee-skills/assessments/{id:long}/reject", ("id", seed.Assessment.Id)),
            seed.Manager.Token,
            cancellationToken: ct
        );

        Assert.Equal((int)SkillAssessmentStatus.Rejected, json.GetProperty("status").GetInt32());
    
    }
}
