using Application.Features.CareerPaths.Queries;
using Application.Shared.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.CareerPaths.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.CareerTests)]
public sealed class GetEmployeeCareerPathQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetEmployeeCareerPath_ShouldCalculateSkillFirstReadiness()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
        SetTenantContext(tenant.Id, user.Id, null, TenantRole.Employee);

        var employee = new Employee
        {
            UserId = user.Id,
            EmployeeNumber = "E-1",
            FirstName = "Ada",
            LastName = "Lovelace",
        };
        var category = new SkillCategory { Name = "Engineering" };
        var skillA = new Skill { SkillCategoryId = 1, Name = "Architecture", IsActive = true };
        var skillB = new Skill { SkillCategoryId = 1, Name = "Stakeholders", IsActive = true };
        PersistWithDatabase(db => db.AddRange(employee, category));
        skillA.SkillCategoryId = category.Id;
        skillB.SkillCategoryId = category.Id;
        PersistWithDatabase(db => db.AddRange(skillA, skillB));

        var tenantLevel2 = new SkillLevel
        {
            Order = 2,
            Name = "Advanced",
            PointsThreshold = 50,
        };
        var tenantLevel4 = new SkillLevel
        {
            Order = 4,
            Name = "Lead",
            PointsThreshold = 150,
        };
        PersistWithDatabase(db => db.AddRange(tenantLevel2, tenantLevel4));

        var family = new JobFamily { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(family));
        var level1 = new CareerLevel { JobFamilyId = family.Id, Order = 1, Name = "IC1" };
        var level2 = new CareerLevel { JobFamilyId = family.Id, Order = 2, Name = "IC2" };
        PersistWithDatabase(db => db.AddRange(level1, level2));
        var currentRole = new RoleProfile
        {
            JobFamilyId = family.Id,
            CareerLevelId = level1.Id,
            Name = "Engineer I",
        };
        var targetRole = new RoleProfile
        {
            JobFamilyId = family.Id,
            CareerLevelId = level2.Id,
            Name = "Engineer II",
        };
        PersistWithDatabase(db => db.AddRange(currentRole, targetRole));
        PersistWithDatabase(db =>
            db.AddRange(
                new EmployeeRoleProfile
                {
                    EmployeeId = employee.Id,
                    RoleProfileId = currentRole.Id,
                    IsActive = true,
                },
                new RoleProfileSkillRequirement
                {
                    RoleProfileId = targetRole.Id,
                    SkillId = skillA.Id,
                    RequiredSkillLevelId = tenantLevel4.Id,
                    Weight = 1,
                },
                new RoleProfileSkillRequirement
                {
                    RoleProfileId = targetRole.Id,
                    SkillId = skillB.Id,
                    RequiredSkillLevelId = tenantLevel4.Id,
                    Weight = 1,
                },
                new EmployeeSkillAssessment
                {
                    EmployeeId = employee.Id,
                    SkillId = skillA.Id,
                    ClaimedSkillLevelId = tenantLevel2.Id,
                    ValidatedSkillLevelId = tenantLevel2.Id,
                    Status = SkillAssessmentStatus.Validated,
                }
            )
        );

        var result = await Send(
            new GetEmployeeCareerPath.GetEmployeeCareerPathQuery(employee.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(targetRole.Id, result.Data!.TargetRole!.Id);
        Assert.Equal(25, result.Data.SkillFitScore);
        Assert.Equal(50, result.Data.ValidationCoverageScore);
        Assert.Equal(29, result.Data.ReadinessScore);
        Assert.Equal(CareerReadinessBand.AtRisk, result.Data.Band);
        Assert.Equal(2, result.Data.SkillGaps.Count);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.CareerTests)]
public sealed class TeamSkillRequirementProviderTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task Provider_ShouldReturnConfiguredTeamRequirements()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);

        var department = new Department { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(department));
        var team = new Team { Name = "Backend", DepartmentId = department.Id };
        var category = new SkillCategory { Name = "Engineering" };
        PersistWithDatabase(db => db.AddRange(team, category));
        var skill = new Skill
        {
            SkillCategoryId = category.Id,
            Name = "C#",
            IsActive = true,
        };
        PersistWithDatabase(db => db.Add(skill));
        var level = new SkillLevel
        {
            Order = 3,
            Name = "Senior",
            PointsThreshold = 100,
        };
        PersistWithDatabase(db => db.Add(level));
        PersistWithDatabase(db =>
            db.Add(
                new TeamSkillRequirement
                {
                    TeamId = team.Id,
                    SkillId = skill.Id,
                    RequiredSkillLevelId = level.Id,
                    Weight = 2,
                }
            )
        );

        using var scope = ServiceProvider.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ITeamSkillRequirementProvider>();

        var result = await provider.GetRequirementsForTeamAsync(
            team.Id,
            TestContext.Current.CancellationToken
        );

        var requirement = Assert.Single(result);
        Assert.Equal(skill.Id, requirement.SkillId);
        Assert.Equal(level.Id, requirement.RequiredSkillLevelId);
        Assert.Equal(2, requirement.Weight);
    }
}
