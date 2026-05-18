using Application.Features.Employees.Queries;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Employees.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class ListEmployeesQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ListEmployees_WithActiveFilter_ShouldOnlyReturnActiveRows()
    {
        var tenant = SeedTenant();
        var activeUser = SeedUser("Active");
        var inactiveUser = SeedUser("Inactive");
        SetTenantContext(tenant.Id);
        PersistWithDatabase(db =>
            db.AddRange(
                new Employee
                {
                    UserId = activeUser.Id,
                    EmployeeNumber = "E-1",
                    FirstName = "Active",
                    LastName = "Employee",
                    IsActive = true,
                },
                new Employee
                {
                    UserId = inactiveUser.Id,
                    EmployeeNumber = "E-2",
                    FirstName = "Inactive",
                    LastName = "Employee",
                    IsActive = false,
                }
            )
        );

        var result = await Send(
            new ListEmployees.ListEmployeesQuery(null, null, true, Page: 1, PageSize: 10),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!.Items);
        Assert.True(result.Data.Items.Single().IsActive);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class GetMyDirectReportsQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetMyDirectReports_ShouldReturnEmployeesManagedByActor()
    {
        var tenant = SeedTenant();
        var managerUser = SeedUser("Manager");
        var reportUser = SeedUser("Report");
        SetTenantContext(tenant.Id);
        var manager = new Employee
        {
            UserId = managerUser.Id,
            EmployeeNumber = "M-1",
            FirstName = "Manager",
            LastName = "User",
        };
        PersistWithDatabase(db => db.Add(manager));
        PersistWithDatabase(db =>
            db.Add(
                new Employee
                {
                    UserId = reportUser.Id,
                    EmployeeNumber = "R-1",
                    FirstName = "Report",
                    LastName = "User",
                    ManagerEmployeeId = manager.Id,
                }
            )
        );

        var result = await Send(
            new GetMyDirectReports.GetMyDirectReportsQuery(managerUser.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!);
        Assert.Equal("Report", result.Data![0].FirstName);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class GetEmployeeQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetEmployee_WithExistingEmployee_ShouldReturnEmployee()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
        SetTenantContext(tenant.Id);
        var employee = new Employee
        {
            UserId = user.Id,
            EmployeeNumber = "E-1",
            FirstName = "Readable",
            LastName = "Employee",
        };
        PersistWithDatabase(db => db.Add(employee));

        var result = await Send(
            new GetEmployee.GetEmployeeQuery(employee.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(employee.Id, result.Data!.Id);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class GetMyProfileQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetMyProfile_WithExistingEmployeeForUser_ShouldReturnProfile()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
        SetTenantContext(tenant.Id, user.Id);
        PersistWithDatabase(db =>
            db.Add(
                new Employee
                {
                    UserId = user.Id,
                    EmployeeNumber = "E-1",
                    FirstName = "Profile",
                    LastName = "Owner",
                }
            )
        );

        var result = await Send(
            new GetMyProfile.GetMyProfileQuery(user.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal("Profile", result.Data!.FirstName);
    }
}
