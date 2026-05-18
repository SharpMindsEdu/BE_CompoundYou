using Domain.Enums;
using Application.Features.EmployeeSkills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class GetMyMatrixEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetMyMatrix_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetMyMatrix.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetMyMatrix_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        Assert.NotNull(ctx.Employee);
        var skill = await SeedSkillAsync(ctx.Tenant, cancellationToken: ct);
        var level = await SeedSkillLevelAsync(ctx.Tenant, cancellationToken: ct);
        var assessment = await SeedEmployeeSkillAssessmentAsync(
            ctx.Tenant,
            ctx.Employee,
            skill,
            level,
            validatedSkillLevel: level,
            status: SkillAssessmentStatus.Validated,
            cancellationToken: ct
        );

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/employee-skills/my-matrix",
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, assessment.Id);
    
    }
}
