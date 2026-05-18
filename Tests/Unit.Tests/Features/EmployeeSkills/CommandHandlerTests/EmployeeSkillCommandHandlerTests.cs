using Application.Features.EmployeeSkills.Commands;
using Application.Shared;
using Application.Shared.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.EmployeeSkills.CommandHandlerTests;

public abstract class EmployeeSkillCommandHandlerTestBase(
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
public sealed class SubmitAssessmentCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : EmployeeSkillCommandHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task SubmitAssessment_WithEmployeeContext_ShouldCreatePendingAssessment()
    {
        var (_, employee, skill, beginner, _) = SeedMatrix();
        SetTenantContext(employee.TenantId, employee.UserId, employee.Id, TenantRole.Employee);

        var result = await Send(
            new SubmitAssessment.SubmitAssessmentCommand(skill.Id, beginner.Id, "Project evidence"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(employee.Id, result.Data!.EmployeeId);
        Assert.Equal(SkillAssessmentStatus.PendingValidation, result.Data.Status);
        Assert.Equal("Project evidence", result.Data.Evidence);
    }

    [Fact]
    public async Task SubmitAssessment_WithExistingAssessment_ShouldUpdateAndResetValidation()
    {
        var (_, employee, skill, beginner, advanced) = SeedMatrix();
        SetTenantContext(employee.TenantId, employee.UserId, employee.Id, TenantRole.Employee);
        PersistWithDatabase(db =>
            db.Add(
                new EmployeeSkillAssessment
                {
                    EmployeeId = employee.Id,
                    SkillId = skill.Id,
                    ClaimedSkillLevelId = beginner.Id,
                    ValidatedSkillLevelId = beginner.Id,
                    ValidatedByEmployeeId = employee.Id,
                    ValidatedOn = DateTimeOffset.UtcNow,
                    Status = SkillAssessmentStatus.Validated,
                    Evidence = "Old evidence",
                }
            )
        );

        var result = await Send(
            new SubmitAssessment.SubmitAssessmentCommand(skill.Id, advanced.Id, "New evidence"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(advanced.Id, result.Data!.ClaimedSkillLevelId);
        Assert.Equal(SkillAssessmentStatus.PendingValidation, result.Data.Status);
        Assert.Null(result.Data.ValidatedSkillLevelId);
        Assert.Null(result.Data.ValidatedByEmployeeId);
        Assert.Null(result.Data.ValidatedOn);
    }

    [Fact]
    public async Task SubmitAssessment_WithoutEmployeeProfile_ShouldReturnNotFound()
    {
        var (_, employee, skill, beginner, _) = SeedMatrix();
        var missingEmployeeUser = SeedUser("MissingEmployee");
        SetTenantContext(employee.TenantId, missingEmployeeUser.Id, null, TenantRole.Employee);

        var result = await Send(
            new SubmitAssessment.SubmitAssessmentCommand(skill.Id, beginner.Id, null),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SubmitAssessment_ShouldCreateNewAssessment()
    {
        var (_, employee, skill, level, _) = SeedMatrix();
        SetTenantContext(employee.TenantId, employee.UserId, employee.Id, TenantRole.Employee);

        var result = await Send(
            new SubmitAssessment.SubmitAssessmentCommand(skill.Id, level.Id, "Years of experience"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(employee.Id, result.Data!.EmployeeId);
        Assert.Equal(SkillAssessmentStatus.PendingValidation, result.Data.Status);
        Assert.Equal("Years of experience", result.Data.Evidence);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class ValidateAssessmentCommandHandlerTests : EmployeeSkillCommandHandlerTestBase
{
    private readonly Mock<IMatrixNotificationService> _notificationServiceMock = new();

    public ValidateAssessmentCommandHandlerTests(
        PostgreSqlRepositoryTestDatabaseFixture fixture,
        ITestOutputHelper outputHelper
    )
        : base(fixture, outputHelper)
    {
        var notificationDescriptor = Services.FirstOrDefault(d =>
            d.ServiceType == typeof(IMatrixNotificationService)
        );
        if (notificationDescriptor is not null)
            Services.Remove(notificationDescriptor);
        Services.AddScoped(_ => _notificationServiceMock.Object);
    }

    [Fact]
    public async Task ValidateAssessment_AsManagerOfEmployee_ShouldMarkAssessmentValidated()
    {
        var (manager, employee, skill, beginner, advanced) = SeedMatrixWithManager();
        var assessment = new EmployeeSkillAssessment
        {
            EmployeeId = employee.Id,
            SkillId = skill.Id,
            ClaimedSkillLevelId = beginner.Id,
            Status = SkillAssessmentStatus.PendingValidation,
        };
        PersistWithDatabase(db => db.Add(assessment));
        SetTenantContext(manager.TenantId, manager.UserId, manager.Id, TenantRole.Manager);

        var result = await Send(
            new ValidateAssessment.ValidateAssessmentCommand(assessment.Id, advanced.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(SkillAssessmentStatus.Validated, result.Data!.Status);
        Assert.Equal(advanced.Id, result.Data.ValidatedSkillLevelId);
        Assert.Equal(manager.Id, result.Data.ValidatedByEmployeeId);
        Assert.NotNull(result.Data.ValidatedOn);
    }

    [Fact]
    public async Task ValidateAssessment_ForOwnAssessment_ShouldReturnForbidden()
    {
        var (_, employee, skill, beginner, _) = SeedMatrix();
        var assessment = new EmployeeSkillAssessment
        {
            EmployeeId = employee.Id,
            SkillId = skill.Id,
            ClaimedSkillLevelId = beginner.Id,
            Status = SkillAssessmentStatus.PendingValidation,
        };
        PersistWithDatabase(db => db.Add(assessment));
        SetTenantContext(employee.TenantId, employee.UserId, employee.Id, TenantRole.Manager);

        var result = await Send(
            new ValidateAssessment.ValidateAssessmentCommand(assessment.Id),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Contains("own assessments", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAssessment_ShouldUpdateStatusAndNotify()
    {
        var (manager, employee, skill, level, _) = SeedMatrixWithManager();
        var assessment = new EmployeeSkillAssessment
        {
            EmployeeId = employee.Id,
            SkillId = skill.Id,
            ClaimedSkillLevelId = level.Id,
            Status = SkillAssessmentStatus.PendingValidation,
        };
        PersistWithDatabase(db => db.Add(assessment));
        SetTenantContext(manager.TenantId, manager.UserId, manager.Id, TenantRole.Manager);

        var result = await Send(
            new ValidateAssessment.ValidateAssessmentCommand(assessment.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(SkillAssessmentStatus.Validated, result.Data!.Status);

        _notificationServiceMock.Verify(
            n => n.NotifyAssessmentValidatedAsync(
                assessment.Id,
                employee.UserId,
                employee.TeamId,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class RejectAssessmentCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : EmployeeSkillCommandHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task RejectAssessment_AsManagerOfEmployee_ShouldMarkAssessmentRejected()
    {
        var (manager, employee, skill, beginner, _) = SeedMatrixWithManager();
        var assessment = new EmployeeSkillAssessment
        {
            EmployeeId = employee.Id,
            SkillId = skill.Id,
            ClaimedSkillLevelId = beginner.Id,
            Status = SkillAssessmentStatus.PendingValidation,
        };
        PersistWithDatabase(db => db.Add(assessment));
        SetTenantContext(manager.TenantId, manager.UserId, manager.Id, TenantRole.Manager);

        var result = await Send(
            new RejectAssessment.RejectAssessmentCommand(assessment.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(SkillAssessmentStatus.Rejected, result.Data!.Status);
        Assert.Equal(manager.Id, result.Data.ValidatedByEmployeeId);
    }
}
