using Application.Shared.Services;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Skills.ServiceTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class SkillCatalogServiceTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    public override async ValueTask InitializeAsync()
    {
        Services.AddMemoryCache();
        await base.InitializeAsync();
    }

    [Fact]
    public async Task GetGlobalSkillsAsync_ShouldReturnOnlyActiveGlobalSkillsWithLevels()
    {
        SetTenantContext(null, isPlatformAdmin: true);
        var category = new SkillCategory { Name = "Global Category", TenantId = null };
        var activeGlobalSkill = new Skill
        {
            SkillCategory = category,
            Name = "C#",
            TenantId = null,
            IsActive = true,
        };
        var inactiveGlobalSkill = new Skill
        {
            SkillCategory = category,
            Name = "Inactive",
            TenantId = null,
            IsActive = false,
        };
        PersistWithDatabase(db =>
        {
            db.AddRange(activeGlobalSkill, inactiveGlobalSkill);
            db.Add(
                new SkillLevel
                {
                    Skill = activeGlobalSkill,
                    Name = "Beginner",
                    Order = 1,
                    PointsThreshold = 0,
                }
            );
        });

        var service = ResolveSkillCatalogService();

        var result = await service.GetGlobalSkillsAsync(TestContext.Current.CancellationToken);

        var skill = Assert.Single(result);
        Assert.Equal(activeGlobalSkill.Id, skill.Id);
        Assert.Equal("C#", skill.Name);
        Assert.Single(skill.SkillLevels);
        Assert.Equal(category.Id, skill.SkillCategoryId);
    }

    [Fact]
    public async Task GetGlobalSkillsAsync_ShouldCacheResults()
    {
        SetTenantContext(null, isPlatformAdmin: true);
        var category = new SkillCategory { Name = "Global Category", TenantId = null };
        var firstSkill = new Skill
        {
            SkillCategory = category,
            Name = "Cached Skill",
            TenantId = null,
            IsActive = true,
        };
        PersistWithDatabase(db => db.Add(firstSkill));

        var service = ResolveSkillCatalogService();

        var firstResult = await service.GetGlobalSkillsAsync(TestContext.Current.CancellationToken);

        PersistWithDatabase(db =>
            db.Add(
                new Skill
                {
                    SkillCategoryId = category.Id,
                    Name = "Late Skill",
                    TenantId = null,
                    IsActive = true,
                }
            )
        );

        var secondResult = await service.GetGlobalSkillsAsync(TestContext.Current.CancellationToken);

        Assert.Single(firstResult);
        Assert.Single(secondResult);
        Assert.Equal("Cached Skill", secondResult[0].Name);
    }

    [Fact]
    public async Task GetGlobalSkillAsync_ShouldReturnSkillFromCatalogCache()
    {
        SetTenantContext(null, isPlatformAdmin: true);
        var category = new SkillCategory { Name = "Global Category", TenantId = null };
        var skill = new Skill
        {
            SkillCategory = category,
            Name = "Target Skill",
            TenantId = null,
            IsActive = true,
        };
        PersistWithDatabase(db => db.Add(skill));

        var service = ResolveSkillCatalogService();

        var result = await service.GetGlobalSkillAsync(skill.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Target Skill", result!.Name);
    }

    private ISkillCatalogService ResolveSkillCatalogService()
    {
        return ServiceProvider.GetRequiredService<ISkillCatalogService>();
    }
}
