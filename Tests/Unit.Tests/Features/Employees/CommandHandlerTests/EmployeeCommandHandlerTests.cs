using Application.Features.Employees.Commands;
using Application.Shared;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Employees.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class CreateEmployeeCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateEmployee_WithValidData_ShouldPersistTenantScopedEmployee()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
        SetTenantContext(tenant.Id);

        var result = await Send(
            new CreateEmployee.CreateEmployeeCommand(
                user.Id,
                "E-100",
                "Grace",
                "Hopper",
                "grace@example.com",
                null,
                null,
                null,
                null,
                "hr-100"
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal("E-100", result.Data!.EmployeeNumber);

        WithDatabase(db =>
        {
            var employee = db.Set<Employee>().Single();
            Assert.Equal(tenant.Id, employee.TenantId);
            Assert.True(employee.IsActive);
        });
    }

    [Fact]
    public async Task CreateEmployee_WithDuplicateEmployeeNumber_ShouldReturnConflict()
    {
        var tenant = SeedTenant();
        var firstUser = SeedUser("First");
        var secondUser = SeedUser("Second");
        SetTenantContext(tenant.Id);
        PersistWithDatabase(db =>
            db.Add(
                new Employee
                {
                    UserId = firstUser.Id,
                    EmployeeNumber = "E-100",
                    FirstName = "First",
                    LastName = "Employee",
                }
            )
        );

        var result = await Send(
            new CreateEmployee.CreateEmployeeCommand(
                secondUser.Id,
                "E-100",
                "Second",
                "Employee",
                null,
                null,
                null,
                null,
                null,
                null
            ),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(TenancyErrors.EmployeeNumberInUse, result.ErrorMessage);
    }

    [Fact]
    public async Task CreateEmployee_WithMissingFirstName_ShouldThrowValidationException()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(
                new CreateEmployee.CreateEmployeeCommand(
                    1,
                    "E-100",
                    "",
                    "Employee",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null
                ),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("First Name", ex.Message);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class AssignManagerCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task AssignManager_WithCycle_ShouldReturnConflict()
    {
        var tenant = SeedTenant();
        var userA = SeedUser("A");
        var userB = SeedUser("B");
        SetTenantContext(tenant.Id);
        var manager = new Employee
        {
            UserId = userA.Id,
            EmployeeNumber = "E-1",
            FirstName = "Manager",
            LastName = "One",
        };
        PersistWithDatabase(db => db.Add(manager));
        var report = new Employee
        {
            UserId = userB.Id,
            EmployeeNumber = "E-2",
            FirstName = "Report",
            LastName = "Two",
            ManagerEmployeeId = manager.Id,
        };
        PersistWithDatabase(db => db.Add(report));

        var result = await Send(
            new AssignManager.AssignManagerCommand(manager.Id, report.Id),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(TenancyErrors.ManagerCycle, result.ErrorMessage);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class UpdateEmployeeCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task UpdateEmployee_WithExistingEmployee_ShouldChangeProfileFields()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
        SetTenantContext(tenant.Id);
        var employee = new Employee
        {
            UserId = user.Id,
            EmployeeNumber = "E-1",
            FirstName = "Old",
            LastName = "Name",
        };
        PersistWithDatabase(db => db.Add(employee));

        var result = await Send(
            new UpdateEmployee.UpdateEmployeeCommand(
                employee.Id,
                "New",
                "Name",
                "new@example.com",
                null,
                DateOnly.FromDateTime(DateTime.UtcNow.Date)
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal("New", result.Data!.FirstName);
        Assert.Equal("new@example.com", result.Data.Email);
        Assert.NotNull(result.Data.HireDate);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class AssignToTeamCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task AssignToTeam_WithMissingTeam_ShouldReturnNotFound()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
        SetTenantContext(tenant.Id);
        var employee = new Employee
        {
            UserId = user.Id,
            EmployeeNumber = "E-1",
            FirstName = "Teamless",
            LastName = "Employee",
        };
        PersistWithDatabase(db => db.Add(employee));

        var result = await Send(
            new AssignToTeam.AssignToTeamCommand(employee.Id, 404),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(TenancyErrors.TeamNotFound, result.ErrorMessage);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class DeactivateEmployeeCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task DeactivateEmployee_ShouldMarkEmployeeInactive()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
        SetTenantContext(tenant.Id);
        var employee = new Employee
        {
            UserId = user.Id,
            EmployeeNumber = "E-1",
            FirstName = "Active",
            LastName = "Employee",
            IsActive = true,
        };
        PersistWithDatabase(db => db.Add(employee));

        var result = await Send(
            new DeactivateEmployee.DeactivateEmployeeCommand(employee.Id, true),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.False(result.Data!.IsActive);
    }
}
