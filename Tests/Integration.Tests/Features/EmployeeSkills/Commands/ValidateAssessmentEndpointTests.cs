using Domain.Enums;
using Application.Features.EmployeeSkills.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class ValidateAssessmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ValidateAssessment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(ValidateAssessment.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ValidateAssessment_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var seed = await SeedManagerAssessmentAsync(SkillAssessmentStatus.PendingValidation, ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            Route("api/employee-skills/assessments/{id:long}/validate", ("id", seed.Assessment.Id)),
            seed.Manager.Token,
            new { Id = seed.Assessment.Id, ValidatedSkillLevelId = seed.ValidatedLevel.Id },
            ct
        );

        Assert.Equal((int)SkillAssessmentStatus.Validated, json.GetProperty("status").GetInt32());
        Assert.Equal(seed.ValidatedLevel.Id, GetRequiredLong(json, "validatedSkillLevelId"));
    
    }
}
