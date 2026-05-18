using Application.Features.Departments.Queries;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Departments.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.DepartmentTests)]
public sealed class ListDepartmentsQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ListDepartments_ShouldReturnPagedTenantDepartments()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        PersistWithDatabase(db =>
            db.AddRange(
                new Department { Name = "Engineering" },
                new Department { Name = "Product" }
            )
        );

        var result = await Send(
            new ListDepartments.ListDepartmentsQuery(Page: 1, PageSize: 10),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Data!.TotalItems);
        Assert.Contains(result.Data.Items, x => x.Name == "Engineering");
        Assert.Contains(result.Data.Items, x => x.Name == "Product");
    }
}
