using Application.Features.Teams.Queries;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Teams.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class ListTeamsQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ListTeams_WithDepartmentFilter_ShouldOnlyReturnMatchingTeams()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var departmentA = new Department { Name = "Engineering" };
        var departmentB = new Department { Name = "Product" };
        PersistWithDatabase(db => db.AddRange(departmentA, departmentB));
        PersistWithDatabase(db =>
            db.AddRange(
                new Team { Name = "Backend", DepartmentId = departmentA.Id },
                new Team { Name = "Discovery", DepartmentId = departmentB.Id }
            )
        );

        var result = await Send(
            new ListTeams.ListTeamsQuery(departmentA.Id, Page: 1, PageSize: 10),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Backend", result.Data.Items.Single().Name);
    }
}
