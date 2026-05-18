using Application.Features.Skills.Commands;
using Application.Shared;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Skills.CommandHandlerTests;

public abstract class SkillCommandHandlerTestBase(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    protected (SkillCategory Category, Skill Skill) SeedSkill()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var category = new SkillCategory { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(category));
        var skill = new Skill
        {
            SkillCategoryId = category.Id,
            Name = "C#",
            Description = "Language",
            IsActive = true,
        };
        PersistWithDatabase(db => db.Add(skill));
        return (category, skill);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class CreateSkillCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : SkillCommandHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateSkill_WithCategory_ShouldPersistSkillForTenant()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var category = new SkillCategory { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(category));

        var result = await Send(
            new CreateSkill.CreateSkillCommand(category.Id, "C#", "Language"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var skill = db.Set<Skill>().Single();
            Assert.Equal(tenant.Id, skill.TenantId);
            Assert.Equal("C#", skill.Name);
        });
    }

    [Fact]
    public async Task CreateSkill_AsPlatformAdminWithTenantContextAndGlobalScope_ShouldPersistGlobalSkill()
    {
        var tenant = SeedTenant();
        SetTenantContext(null, isPlatformAdmin: true);
        var category = new SkillCategory
        {
            Name = "Global Engineering",
            Description = "Global category",
        };
        PersistWithDatabase(db => db.Add(category));

        SetTenantContext(tenant.Id, isPlatformAdmin: true);

        var result = await Send(
            new CreateSkill.CreateSkillCommand(
                category.Id,
                "Global Architecture",
                "Shared skill",
                IsGlobal: true
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var skill = db.Set<Skill>().IgnoreQueryFilters().Single(x => x.Id == result.Data);
            Assert.Null(skill.TenantId);
            Assert.Equal(category.Id, skill.SkillCategoryId);
        });
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class UpdateSkillCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : SkillCommandHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task UpdateSkill_ForTenantSkill_ShouldUpdateFields()
    {
        var (category, skill) = SeedSkill();
        var replacementCategory = new SkillCategory { Name = "Product" };
        PersistWithDatabase(db => db.Add(replacementCategory));

        var result = await Send(
            new UpdateSkill.UpdateSkillCommand(
                skill.Id,
                replacementCategory.Id,
                "F#",
                "Functional language",
                null,
                false
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal("F#", result.Data!.Name);
        Assert.Equal(replacementCategory.Id, result.Data.SkillCategoryId);
        Assert.False(result.Data.IsActive);
    }
}
