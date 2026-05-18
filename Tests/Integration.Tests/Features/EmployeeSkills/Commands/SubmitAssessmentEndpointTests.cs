using Domain.Enums;
using Application.Features.EmployeeSkills.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class SubmitAssessmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SubmitAssessment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            SubmitAssessment.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task SubmitAssessment_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        Assert.NotNull(ctx.Employee);
        var skill = await SeedSkillAsync(ctx.Tenant, cancellationToken: ct);
        var level = await SeedSkillLevelAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/employee-skills/assessments",
            ctx.Token,
            new { SkillId = skill.Id, ClaimedSkillLevelId = level.Id, Evidence = "Real seeded evidence" },
            ct
        );

        Assert.Equal(ctx.Employee.Id, GetRequiredLong(json, "employeeId"));
        Assert.Equal((int)SkillAssessmentStatus.PendingValidation, json.GetProperty("status").GetInt32());
    
    }
}
