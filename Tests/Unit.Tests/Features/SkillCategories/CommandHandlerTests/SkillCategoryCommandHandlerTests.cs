using Application.Features.SkillCategories.Commands;
using Application.Shared;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.SkillCategories.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class CreateSkillCategoryCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateSkillCategory_WithTenantContext_ShouldPersistTenantCategory()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);

        var result = await Send(
            new CreateSkillCategory.CreateSkillCategoryCommand("Engineering", "Tech skills"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.True(result.Data > 0);
        WithDatabase(db => Assert.Equal(tenant.Id, db.Set<SkillCategory>().Single().TenantId));
    }

    [Fact]
    public async Task CreateSkillCategory_AsPlatformAdminWithTenantContextAndGlobalScope_ShouldPersistGlobalCategory()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id, isPlatformAdmin: true);

        var result = await Send(
            new CreateSkillCategory.CreateSkillCategoryCommand("Global Engineering", "Global rubric", IsGlobal: true),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var category = db.Set<SkillCategory>().IgnoreQueryFilters().Single(x => x.Id == result.Data);
            Assert.Null(category.TenantId);
        });
    }

    [Fact]
    public async Task CreateSkillCategory_WithoutTenantContextAndTenantScope_ShouldReturnForbidden()
    {
        SetTenantContext(null, isPlatformAdmin: true);

        var result = await Send(
            new CreateSkillCategory.CreateSkillCategoryCommand("Tenant Engineering", "Tenant rubric"),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal(TenancyErrors.NoTenantInContext, result.ErrorMessage);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class UpdateSkillCategoryCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task UpdateSkillCategory_ForGlobalCategoryAsTenantAdmin_ShouldReturnForbidden()
    {
        var tenant = SeedTenant();
        SetTenantContext(null, isPlatformAdmin: true);
        var category = new SkillCategory
        {
            Name = "Global",
            Description = "Global catalog",
            TenantId = null,
        };
        PersistWithDatabase(db => db.Add(category));

        SetTenantContext(tenant.Id);
        var result = await Send(
            new UpdateSkillCategory.UpdateSkillCategoryCommand(category.Id, "Changed", null, true),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal(ErrorResults.Forbidden, result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateSkillCategory_ForTenantCategory_ShouldUpdateFields()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var category = new SkillCategory
        {
            Name = "Engineering",
            Description = "Old",
            IsActive = true,
        };
        PersistWithDatabase(db => db.Add(category));

        var result = await Send(
            new UpdateSkillCategory.UpdateSkillCategoryCommand(
                category.Id,
                "Product",
                "New",
                false
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal("Product", result.Data!.Name);
        Assert.Equal("New", result.Data.Description);
        Assert.False(result.Data.IsActive);
    }
}
