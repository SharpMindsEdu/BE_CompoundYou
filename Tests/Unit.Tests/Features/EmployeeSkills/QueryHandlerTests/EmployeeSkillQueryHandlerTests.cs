using Application.Features.EmployeeSkills.Queries;
using Application.Shared.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.EmployeeSkills.QueryHandlerTests;

public abstract class EmployeeSkillQueryHandlerTestBase(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    protected (SkillCategory Category, Employee Employee, Skill Skill, SkillLevel Beginner, SkillLevel Advanced) SeedMatrix()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
        SetTenantContext(tenant.Id, user.Id, null, TenantRole.Employee);

        var category = new SkillCategory { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(category));
        var employee = new Employee
        {
            UserId = user.Id,
            EmployeeNumber = "E-1",
            FirstName = "Ada",
            LastName = "Lovelace",
        };
        var skill = new Skill
        {
            SkillCategoryId = category.Id,
            Name = "C#",
            IsActive = true,
        };
        PersistWithDatabase(db => db.AddRange(employee, skill));
        var beginner = new SkillLevel
        {
            Order = 1,
            Name = "Beginner",
            PointsThreshold = 0,
        };
        var advanced = new SkillLevel
        {
            Order = 2,
            Name = "Advanced",
            PointsThreshold = 50,
        };
        PersistWithDatabase(db => db.AddRange(beginner, advanced));
        return (category, employee, skill, beginner, advanced);
    }

    protected (Employee Manager, Employee Employee, Skill Skill, SkillLevel Beginner, SkillLevel Advanced) SeedMatrixWithManager()
    {
        var (category, employee, skill, beginner, advanced) = SeedMatrix();
        var managerUser = SeedUser("Manager");
        var manager = new Employee
        {
            UserId = managerUser.Id,
            EmployeeNumber = "M-1",
            FirstName = "Manager",
            LastName = "User",
        };
        PersistWithDatabase(db => db.Add(manager));
        employee.ManagerEmployeeId = manager.Id;
        PersistWithDatabase(db => db.Update(employee));
        return (manager, employee, skill, beginner, advanced);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class GetMyMatrixQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : EmployeeSkillQueryHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetMyMatrix_ShouldReturnOnlyCurrentEmployeeAssessments()
    {
        var (_, employee, skill, beginner, advanced) = SeedMatrix();
        var otherUser = SeedUser("Other");
        var otherEmployee = new Employee
        {
            UserId = otherUser.Id,
            EmployeeNumber = "E-OTHER",
            FirstName = "Other",
            LastName = "Employee",
        };
        PersistWithDatabase(db => db.Add(otherEmployee));
        PersistWithDatabase(db =>
            db.AddRange(
                new EmployeeSkillAssessment
                {
                    EmployeeId = employee.Id,
                    SkillId = skill.Id,
                    ClaimedSkillLevelId = beginner.Id,
                    Status = SkillAssessmentStatus.PendingValidation,
                },
                new EmployeeSkillAssessment
                {
                    EmployeeId = otherEmployee.Id,
                    SkillId = skill.Id,
                    ClaimedSkillLevelId = advanced.Id,
                    Status = SkillAssessmentStatus.PendingValidation,
                }
            )
        );
        SetTenantContext(employee.TenantId, employee.UserId, employee.Id, TenantRole.Employee);

        var result = await Send(
            new GetMyMatrix.GetMyMatrixQuery(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!);
        Assert.Equal(employee.Id, result.Data![0].EmployeeId);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class GetEmployeeMatrixQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : EmployeeSkillQueryHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetEmployeeMatrix_AsManagerOfEmployee_ShouldReturnTargetAssessments()
    {
        var (manager, employee, skill, beginner, _) = SeedMatrixWithManager();
        PersistWithDatabase(db =>
            db.Add(
                new EmployeeSkillAssessment
                {
                    EmployeeId = employee.Id,
                    SkillId = skill.Id,
                    ClaimedSkillLevelId = beginner.Id,
                    Status = SkillAssessmentStatus.PendingValidation,
                }
            )
        );
        SetTenantContext(manager.TenantId, manager.UserId, manager.Id, TenantRole.Manager);

        var result = await Send(
            new GetEmployeeMatrix.GetEmployeeMatrixQuery(employee.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!);
        Assert.Equal(employee.Id, result.Data![0].EmployeeId);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class GetSkillGapReportQueryHandlerTests : EmployeeSkillQueryHandlerTestBase
{
    private readonly Mock<ITeamSkillRequirementProvider> _requirementProviderMock = new();

    public GetSkillGapReportQueryHandlerTests(
        PostgreSqlRepositoryTestDatabaseFixture fixture,
        ITestOutputHelper outputHelper
    )
        : base(fixture, outputHelper)
    {
        var requirementDescriptor = Services.FirstOrDefault(d =>
            d.ServiceType == typeof(ITeamSkillRequirementProvider)
        );
        if (requirementDescriptor is not null)
            Services.Remove(requirementDescriptor);
        Services.AddScoped(_ => _requirementProviderMock.Object);
    }

    [Fact(Skip = "Not yet implemented")]
    public async Task GetSkillGapReport_WithTeamButNoRequirements_ShouldReturnEmptyReport()
    {
        var (_, employee, _, _, _) = SeedMatrix();
        var department = new Department { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(department));
        var team = new Team { Name = "Backend", DepartmentId = department.Id };
        PersistWithDatabase(db => db.Add(team));
        employee.TeamId = team.Id;
        PersistWithDatabase(db => db.Update(employee));
        SetTenantContext(employee.TenantId, employee.UserId, employee.Id, TenantRole.Employee);

        var result = await Send(
            new GetSkillGapReport.GetSkillGapReportQuery(employee.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(employee.Id, result.Data!.EmployeeId);
        Assert.Empty(result.Data.Gaps);
    }

    [Fact]
    public async Task GetSkillGapReport_ShouldCompareAgainstRequirements()
    {
        var (_, employee, skill, actualLevel, requiredLevel) = SeedMatrix();
        var department = new Department { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(department));
        var team = new Team { Name = "Backend", DepartmentId = department.Id };
        PersistWithDatabase(db => db.Add(team));
        employee.TeamId = team.Id;
        PersistWithDatabase(db => db.Update(employee));
        PersistWithDatabase(db =>
            db.Add(
                new EmployeeSkillAssessment
                {
                    EmployeeId = employee.Id,
                    SkillId = skill.Id,
                    ClaimedSkillLevelId = actualLevel.Id,
                    ValidatedSkillLevelId = actualLevel.Id,
                    Status = SkillAssessmentStatus.Validated,
                }
            )
        );
        SetTenantContext(employee.TenantId, employee.UserId, employee.Id, TenantRole.Employee);

        _requirementProviderMock
            .Setup(p => p.GetRequirementsForTeamAsync(team.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamSkillRequirementStub> { new(skill.Id, requiredLevel.Id) });

        var result = await Send(
            new GetSkillGapReport.GetSkillGapReportQuery(employee.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.ErrorMessage);
        var gap = Assert.Single(result.Data!.Gaps);
        Assert.Equal(skill.Id, gap.SkillId);
        Assert.Equal(1, gap.ActualLevelOrder);
        Assert.Equal(2, gap.RequiredLevelOrder);
        Assert.Equal(-1, gap.Gap);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class GetTeamHeatmapQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : EmployeeSkillQueryHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetTeamHeatmap_WithValidatedAssessment_ShouldReturnEmployeeSkillRows()
    {
        var (_, employee, skill, beginner, _) = SeedMatrix();
        var department = new Department { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(department));
        var team = new Team { Name = "Backend", DepartmentId = department.Id };
        PersistWithDatabase(db => db.Add(team));
        employee.TeamId = team.Id;
        PersistWithDatabase(db => db.Update(employee));
        PersistWithDatabase(db =>
            db.Add(
                new EmployeeSkillAssessment
                {
                    EmployeeId = employee.Id,
                    SkillId = skill.Id,
                    ClaimedSkillLevelId = beginner.Id,
                    ValidatedSkillLevelId = beginner.Id,
                    Status = SkillAssessmentStatus.Validated,
                }
            )
        );

        var result = await Send(
            new GetTeamHeatmap.GetTeamHeatmapQuery(team.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        var row = Assert.Single(result.Data!.Employees);
        Assert.Equal($"{employee.FirstName} {employee.LastName}", row.DisplayName);
        Assert.Single(row.Skills);
        Assert.Equal(skill.Id, row.Skills[0].SkillId);
    }

    [Fact]
    public async Task GetTeamHeatmap_ShouldReturnAggregatedData()
    {
        var (_, employee, skill, level, _) = SeedMatrix();
        var otherUser = SeedUser("Other");
        var department = new Department { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(department));
        var team = new Team { Name = "Backend", DepartmentId = department.Id };
        PersistWithDatabase(db => db.Add(team));

        employee.TeamId = team.Id;
        PersistWithDatabase(db => db.Update(employee));
        PersistWithDatabase(db =>
            db.AddRange(
                new Employee
                {
                    UserId = otherUser.Id,
                    EmployeeNumber = "E-2",
                    FirstName = "Other",
                    LastName = "Employee",
                    TeamId = team.Id,
                },
                new EmployeeSkillAssessment
                {
                    EmployeeId = employee.Id,
                    SkillId = skill.Id,
                    ClaimedSkillLevelId = level.Id,
                    ValidatedSkillLevelId = level.Id,
                    Status = SkillAssessmentStatus.Validated,
                }
            )
        );

        var result = await Send(
            new GetTeamHeatmap.GetTeamHeatmapQuery(team.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(2, result.Data!.Employees.Count);
        Assert.Contains(result.Data.Employees, x => x.EmployeeId == employee.Id && x.Skills.Count == 1);
    }
}
