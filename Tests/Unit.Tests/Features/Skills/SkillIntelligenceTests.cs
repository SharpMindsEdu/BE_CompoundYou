using System.Security.Claims;
using Application.Authorization;
using Application.Features.SkillCategories.Commands;
using Application.Features.Skills.Commands;
using Application.Features.EmployeeSkills.Commands;
using Application.Shared;
using Domain.Entities;
using Domain.Enums;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Unit.Tests.Base;

using Application.Features.EmployeeSkills.Queries;
using Application.Features.Skills.Queries;
using Application.Shared.Services;

namespace Unit.Tests.Features.Skills;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public class SkillIntelligenceTests : PostgreSqlTestBase<ApplicationDbContext>
{
    private readonly Mock<ICurrentTenant> _currentTenantMock = new();
    private readonly Mock<IMatrixNotificationService> _notificationServiceMock = new();
    private readonly Mock<ITeamSkillRequirementProvider> _reqProviderMock = new();

    public SkillIntelligenceTests(PostgreSqlRepositoryTestDatabaseFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, "Skill")
    {
        var tenantDescriptor = Services.First(d => d.ServiceType == typeof(ICurrentTenant));
        Services.Remove(tenantDescriptor);
        Services.AddScoped(_ => _currentTenantMock.Object);

        var notifyDescriptor = Services.FirstOrDefault(d => d.ServiceType == typeof(IMatrixNotificationService));
        if (notifyDescriptor != null) Services.Remove(notifyDescriptor);
        Services.AddScoped(_ => _notificationServiceMock.Object);

        var reqDescriptor = Services.FirstOrDefault(d => d.ServiceType == typeof(ITeamSkillRequirementProvider));
        if (reqDescriptor != null) Services.Remove(reqDescriptor);
        Services.AddScoped(_ => _reqProviderMock.Object);

        _currentTenantMock.Setup(t => t.IsPlatformAdmin).Returns(true);
    }

    private static ClaimsPrincipal CreatePrincipal(long userId, long tenantId, long membershipId, TenantRole role = TenantRole.Employee)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(CompoundYouClaimTypes.TenantId, tenantId.ToString()),
            new(CompoundYouClaimTypes.MembershipId, membershipId.ToString()),
            new(CompoundYouClaimTypes.TenantRole, role.ToString())
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private long SeedTenant()
    {
        long tenantId = 0;
        PersistWithDatabase(db =>
        {
            var tenant = new Tenant { Name = "Test Tenant", Slug = Guid.NewGuid().ToString() };
            db.Set<Tenant>().Add(tenant);
            db.SaveChanges();
            tenantId = tenant.Id;
        });
        return tenantId;
    }

    [Fact]
    public async Task SubmitAssessment_ShouldCreateNewAssessment()
    {
        // Arrange
        var tenantId = SeedTenant();
        long skillId = 0;
        long levelId = 0;
        long employeeId = 0;

        using (var scope = ServiceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = db.Set<Tenant>().Find(tenantId)!;
            var user = new User { DisplayName = "Submitter", Email = Guid.NewGuid() + "@test.com" };
            var cat = new SkillCategory { Name = "Cat", Tenant = tenant };
            var skill = new Skill { Name = "C#", SkillCategory = cat, Tenant = tenant };
            var level = new SkillLevel { Skill = skill, Name = "Senior", Order = 3 };
            var emp = new Employee { Tenant = tenant, FirstName = "E", LastName = "1", EmployeeNumber = "E1", User = user };
            
            db.Set<Employee>().Add(emp);
            db.Set<SkillLevel>().Add(level);
            db.SaveChanges();
            
            skillId = skill.Id;
            levelId = level.Id;
            employeeId = emp.Id;
        }

        _currentTenantMock.Setup(t => t.TenantId).Returns(tenantId);
        _currentTenantMock.Setup(t => t.IsPlatformAdmin).Returns(false);
        _currentTenantMock.Setup(t => t.MembershipId).Returns(employeeId);

        var command = new SubmitAssessment.SubmitAssessmentCommand(skillId, levelId, "Years of experience");

        // Act
        var result = await Send(command);

        // Assert
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(SkillAssessmentStatus.PendingValidation, result.Data!.Status);
    }

    [Fact]
    public async Task ValidateAssessment_ShouldUpdateStatusAndNotify()
    {
        // Arrange
        var tenantId = SeedTenant();
        long managerId = 0;
        long employeeId = 0;
        long assessmentId = 0;
        long managerUserId = 0;

        PersistWithDatabase(db =>
        {
            var tenant = db.Set<Tenant>().Find(tenantId)!;
            var user1 = new User { DisplayName = "Manager", Email = "m@test.com" };
            var user2 = new User { DisplayName = "Employee", Email = "e@test.com" };
            var manager = new Employee { Tenant = tenant, FirstName = "Boss", LastName = "Man", EmployeeNumber = "M1", User = user1 };
            var emp = new Employee { Tenant = tenant, FirstName = "Worker", LastName = "Bee", EmployeeNumber = "E1", User = user2, ManagerEmployee = manager };
            var cat = new SkillCategory { Name = "Cat", Tenant = tenant };
            var skill = new Skill { Name = "C#", SkillCategory = cat, Tenant = tenant };
            var level = new SkillLevel { Skill = skill, Name = "Level", Order = 1 };

            var assessment = new EmployeeSkillAssessment
            {
                Employee = emp,
                Skill = skill,
                ClaimedSkillLevel = level,
                Status = SkillAssessmentStatus.PendingValidation,
                Tenant = tenant
            };
            
            db.Set<EmployeeSkillAssessment>().Add(assessment);
            db.SaveChanges();

            managerId = manager.Id;
            employeeId = emp.Id;
            assessmentId = assessment.Id;
            managerUserId = user1.Id;
        });

        _currentTenantMock.Setup(t => t.TenantId).Returns(tenantId);
        _currentTenantMock.Setup(t => t.IsPlatformAdmin).Returns(false);
        _currentTenantMock.Setup(t => t.MembershipId).Returns(managerId);
        _currentTenantMock.Setup(t => t.UserId).Returns(managerUserId);
        _currentTenantMock.Setup(t => t.User).Returns(CreatePrincipal(managerUserId, tenantId, managerId, TenantRole.Manager));

        var command = new ValidateAssessment.ValidateAssessmentCommand(assessmentId);

        // Act
        var result = await Send(command);

        // Assert
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(SkillAssessmentStatus.Validated, result.Data!.Status);
        
        _notificationServiceMock.Verify(n => n.NotifyAssessmentValidatedAsync(assessmentId, It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTeamHeatmap_ShouldReturnAggregatedData()
    {
        // Arrange
        var tenantId = SeedTenant();
        _currentTenantMock.Setup(t => t.TenantId).Returns(tenantId);
        _currentTenantMock.Setup(t => t.IsPlatformAdmin).Returns(false);

        long teamId = 0;

        PersistWithDatabase(db =>
        {
            var tenant = db.Set<Tenant>().Find(tenantId)!;
            var dept = new Department { Name = "D1", Tenant = tenant };
            var team = new Team { Name = "T1", Tenant = tenant, Department = dept };
            var user1 = new User { DisplayName = "User 1", Email = "u1@test.com" };
            var user2 = new User { DisplayName = "User 2", Email = "u2@test.com" };
            var cat = new SkillCategory { Name = "Cat", Tenant = tenant };
            var skill = new Skill { Name = "Skill A", SkillCategory = cat, Tenant = tenant };
            var level = new SkillLevel { Skill = skill, Name = "L1", Order = 1 };

            var emp1 = new Employee { Team = team, Tenant = tenant, FirstName = "A", LastName = "1", EmployeeNumber = "1", User = user1 };
            var emp2 = new Employee { Team = team, Tenant = tenant, FirstName = "B", LastName = "2", EmployeeNumber = "2", User = user2 };
            var a1 = new EmployeeSkillAssessment { Employee = emp1, Skill = skill, ClaimedSkillLevel = level, Status = SkillAssessmentStatus.Validated, ValidatedSkillLevel = level, Tenant = tenant };
            
            db.Set<EmployeeSkillAssessment>().Add(a1);
            db.Set<Employee>().Add(emp2);
            db.SaveChanges();
            
            teamId = team.Id;
        });

        // Act
        var result = await Send(new GetTeamHeatmap.GetTeamHeatmapQuery(teamId));

        // Assert
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(2, result.Data!.Employees.Count);
    }

    [Fact]
    public async Task GetSkillGapReport_ShouldCompareAgainstRequirements()
    {
        // Arrange
        var tenantId = SeedTenant();
        _currentTenantMock.Setup(t => t.TenantId).Returns(tenantId);
        
        long teamId = 0;
        long employeeId = 0;
        long skillId = 0;
        long level2Id = 0;
        long userId = 0;

        PersistWithDatabase(db =>
        {
            var tenant = db.Set<Tenant>().Find(tenantId)!;
            var user = new User { DisplayName = "User Gap", Email = "gap@test.com" };
            var dept = new Department { Name = "D1", Tenant = tenant };
            var team = new Team { Name = "T1", Tenant = tenant, Department = dept };
            var emp = new Employee { Team = team, Tenant = tenant, FirstName = "A", LastName = "1", EmployeeNumber = "1", User = user };
            var skill = new Skill { Name = "Skill A", Tenant = tenant, SkillCategory = new SkillCategory { Name = "C", Tenant = tenant } };
            var level1 = new SkillLevel { Skill = skill, Order = 1, Name = "L1" };
            var level2 = new SkillLevel { Skill = skill, Order = 2, Name = "L2" };
            var a = new EmployeeSkillAssessment { Employee = emp, Skill = skill, ClaimedSkillLevel = level1, Status = SkillAssessmentStatus.Validated, ValidatedSkillLevel = level1, Tenant = tenant };

            db.Set<EmployeeSkillAssessment>().Add(a);
            db.Set<SkillLevel>().Add(level2);
            db.SaveChanges();

            employeeId = emp.Id;
            skillId = skill.Id;
            level2Id = level2.Id;
            teamId = team.Id;
            userId = user.Id;
        });

        _currentTenantMock.Setup(t => t.IsPlatformAdmin).Returns(false);
        _currentTenantMock.Setup(t => t.User).Returns(CreatePrincipal(userId, tenantId, employeeId));

        _reqProviderMock.Setup(p => p.GetRequirementsForTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamSkillRequirementStub> { new(skillId, level2Id) });

        // Act
        var result = await Send(new GetSkillGapReport.GetSkillGapReportQuery(employeeId));

        // Assert
        Assert.True(result.Succeeded, result.ErrorMessage);
        var gap = result.Data!.Gaps.First(g => g.SkillId == skillId);
        Assert.Equal(1, gap.ActualLevelOrder);
        Assert.Equal(2, gap.RequiredLevelOrder);
        Assert.Equal(-1, gap.Gap);
    }

    [Fact]
    public async Task CreateSkillCategory_ShouldPersistToDatabase()
    {
        // Arrange
        var tenantId = SeedTenant();
        _currentTenantMock.Setup(t => t.TenantId).Returns(tenantId);
        _currentTenantMock.Setup(t => t.IsPlatformAdmin).Returns(false);

        var command = new CreateSkillCategory.CreateSkillCategoryCommand("Backend", "Skills for backend devs");

        // Act
        var result = await Send(command);

        // Assert
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.Data > 0);

        WithDatabase(db =>
        {
            var category = db.Set<SkillCategory>().Find(result.Data);
            Assert.NotNull(category);
            Assert.Equal("Backend", category.Name);
            Assert.Equal(tenantId, category.TenantId);
        });
    }
}
