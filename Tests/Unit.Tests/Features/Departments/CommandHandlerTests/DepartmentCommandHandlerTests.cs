using Application.Features.Departments.Commands;
using Application.Shared;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Departments.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.DepartmentTests)]
public sealed class CreateDepartmentCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateDepartment_WithTenantContext_ShouldPersistDepartment()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);

        var result = await Send(
            new CreateDepartment.CreateDepartmentCommand("Engineering", null),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("Engineering", result.Data.Name);

        WithDatabase(db =>
        {
            var department = db.Set<Department>().Single();
            Assert.Equal(tenant.Id, department.TenantId);
        });
    }

    [Fact]
    public async Task CreateDepartment_WithMissingParent_ShouldReturnNotFound()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);

        var result = await Send(
            new CreateDepartment.CreateDepartmentCommand("Platform", 999),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(TenancyErrors.DepartmentNotFound, result.ErrorMessage);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.DepartmentTests)]
public sealed class UpdateDepartmentCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task UpdateDepartment_WithSelfParent_ShouldThrowValidationException()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var department = new Department { Name = "People" };
        PersistWithDatabase(db => db.Add(department));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(
                new UpdateDepartment.UpdateDepartmentCommand(
                    department.Id,
                    "People Ops",
                    department.Id
                ),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Contains("own parent", ex.Message);
    }

    [Fact]
    public async Task UpdateDepartment_WithExistingParent_ShouldUpdateNameAndParent()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var parent = new Department { Name = "Engineering" };
        var child = new Department { Name = "Backend" };
        PersistWithDatabase(db => db.AddRange(parent, child));

        var result = await Send(
            new UpdateDepartment.UpdateDepartmentCommand(child.Id, "Platform", parent.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal("Platform", result.Data!.Name);
        Assert.Equal(parent.Id, result.Data.ParentDepartmentId);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.DepartmentTests)]
public sealed class DeleteDepartmentCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task DeleteDepartment_WithExistingDepartment_ShouldRemoveIt()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var department = new Department { Name = "Temporary" };
        PersistWithDatabase(db => db.Add(department));

        var result = await Send(
            new DeleteDepartment.DeleteDepartmentCommand(department.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        WithDatabase(db => Assert.Empty(db.Set<Department>()));
    }
}
