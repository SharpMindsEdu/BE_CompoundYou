using Application.Features.SkillCategories.Queries;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.SkillCategories.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class ListSkillCategoriesQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ListSkillCategories_ShouldReturnOnlyActiveVisibleCategories()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        PersistWithDatabase(db =>
            db.AddRange(
                new SkillCategory { Name = "Active", IsActive = true },
                new SkillCategory { Name = "Inactive", IsActive = false }
            )
        );

        var result = await Send(
            new ListSkillCategories.ListSkillCategoriesQuery(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!);
        Assert.Equal("Active", result.Data![0].Name);
    }
}
