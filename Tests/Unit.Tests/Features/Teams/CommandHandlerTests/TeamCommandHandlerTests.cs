using Application.Features.Teams.Commands;
using Application.Shared;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Teams.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class CreateTeamCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateTeam_WithExistingDepartment_ShouldPersistTeam()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var department = new Department { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(department));

        var result = await Send(
            new CreateTeam.CreateTeamCommand("Backend", department.Id, null),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("Backend", result.Data.Name);

        WithDatabase(db =>
        {
            var team = db.Set<Team>().Single();
            Assert.Equal(tenant.Id, team.TenantId);
            Assert.Equal(department.Id, team.DepartmentId);
        });
    }

    [Fact]
    public async Task CreateTeam_WithMissingDepartment_ShouldReturnNotFound()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);

        var result = await Send(
            new CreateTeam.CreateTeamCommand("Backend", 999, null),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(TenancyErrors.DepartmentNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task CreateTeam_WithEmptyName_ShouldThrowValidationException()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(new CreateTeam.CreateTeamCommand("", 1, null), TestContext.Current.CancellationToken)
        );

        Assert.Contains("Name", ex.Message);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class SetTeamManagerCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task SetTeamManager_WithExistingEmployee_ShouldAssignManager()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var user = SeedUser();
        var department = new Department { Name = "Engineering" };
        var manager = new Employee
        {
            UserId = user.Id,
            EmployeeNumber = "E-1",
            FirstName = "Ada",
            LastName = "Lovelace",
        };
        PersistWithDatabase(db => db.AddRange(department, manager));
        var team = new Team { Name = "Backend", DepartmentId = department.Id };
        PersistWithDatabase(db => db.Add(team));

        var result = await Send(
            new SetTeamManager.SetTeamManagerCommand(team.Id, manager.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(manager.Id, result.Data!.ManagerEmployeeId);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class UpdateTeamCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task UpdateTeam_WithExistingDepartment_ShouldChangeNameAndDepartment()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var departmentA = new Department { Name = "Engineering" };
        var departmentB = new Department { Name = "Product" };
        PersistWithDatabase(db => db.AddRange(departmentA, departmentB));
        var team = new Team { Name = "Backend", DepartmentId = departmentA.Id };
        PersistWithDatabase(db => db.Add(team));

        var result = await Send(
            new UpdateTeam.UpdateTeamCommand(team.Id, "Platform", departmentB.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal("Platform", result.Data!.Name);
        Assert.Equal(departmentB.Id, result.Data.DepartmentId);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class DeleteTeamCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task DeleteTeam_WithMissingTeam_ShouldReturnNotFound()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);

        var result = await Send(
            new DeleteTeam.DeleteTeamCommand(404),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(TenancyErrors.TeamNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteTeam_WithExistingTeam_ShouldRemoveIt()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var department = new Department { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(department));
        var team = new Team { Name = "Temporary", DepartmentId = department.Id };
        PersistWithDatabase(db => db.Add(team));

        var result = await Send(
            new DeleteTeam.DeleteTeamCommand(team.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        WithDatabase(db => Assert.Empty(db.Set<Team>()));
    }
}
