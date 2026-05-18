using Domain.Enums;
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

    [Fact]
    public async Task GetSkillGapReport_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        Assert.NotNull(ctx.Employee);
        var team = await SeedTeamAsync(ctx.Tenant, cancellationToken: ct);
        await SetEmployeeTeamAsync(ctx.Tenant, ctx.Employee, team, ct);
        var skill = await SeedSkillAsync(ctx.Tenant, cancellationToken: ct);
        var levelOne = await SeedSkillLevelAsync(ctx.Tenant, order: 1, cancellationToken: ct);
        var levelTwo = await SeedSkillLevelAsync(ctx.Tenant, order: 2, cancellationToken: ct);
        await SeedTeamSkillRequirementAsync(ctx.Tenant, team, skill, levelTwo, cancellationToken: ct);
        await SeedEmployeeSkillAssessmentAsync(
            ctx.Tenant,
            ctx.Employee,
            skill,
            levelOne,
            validatedSkillLevel: levelOne,
            status: SkillAssessmentStatus.Validated,
            cancellationToken: ct
        );

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/employee-skills/gap-report/{employeeId:long}", ("employeeId", ctx.Employee.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.Equal(ctx.Employee.Id, GetRequiredLong(json, "employeeId"));
        Assert.NotEmpty(json.GetProperty("gaps").EnumerateArray());
    
    }
}
