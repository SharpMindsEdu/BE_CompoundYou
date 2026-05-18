using Application.Features.Skills.Commands;
using Application.Shared;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Skills.CommandHandlerTests;

public abstract class SkillCommandHandlerTestBase(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    protected (SkillCategory Category, Skill Skill) SeedSkillWithLevels()
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
        PersistWithDatabase(db =>
            db.AddRange(
                new SkillLevel
                {
                    SkillId = skill.Id,
                    Order = 1,
                    Name = "Beginner",
                    PointsThreshold = 0,
                },
                new SkillLevel
                {
                    SkillId = skill.Id,
                    Order = 2,
                    Name = "Advanced",
                    PointsThreshold = 50,
                }
            )
        );
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
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class AddSkillLevelCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : SkillCommandHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task AddSkillLevel_ShouldAppendNextOrder()
    {
        var (_, skill) = SeedSkillWithLevels();

        var result = await Send(
            new AddSkillLevel.AddSkillLevelCommand(skill.Id, "Expert", "Can mentor others", 100),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Data!.Order);
        Assert.Equal("Expert", result.Data.Name);
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
        var (category, skill) = SeedSkillWithLevels();
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

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class ReorderSkillLevelsCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : SkillCommandHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ReorderSkillLevels_WithUnknownLevel_ShouldReturnBadRequest()
    {
        var (_, skill) = SeedSkillWithLevels();

        var result = await Send(
            new ReorderSkillLevels.ReorderSkillLevelsCommand(skill.Id, new List<long> { 999 }),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.BadRequest, result.Status);
        Assert.Contains("Invalid level IDs", result.ErrorMessage);
    }

    [Fact]
    public async Task ReorderSkillLevels_WithAllExistingLevels_ShouldPersistNewOrder()
    {
        var (_, skill) = SeedSkillWithLevels();
        List<SkillLevel> levels = null!;
        WithDatabase(db => levels = db.Set<SkillLevel>().OrderBy(x => x.Order).ToList());

        var result = await Send(
            new ReorderSkillLevels.ReorderSkillLevelsCommand(
                skill.Id,
                new List<long> { levels[1].Id, levels[0].Id }
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { levels[1].Id, levels[0].Id }, result.Data!.Select(x => x.Id));
        WithDatabase(db =>
        {
            var stored = db.Set<SkillLevel>().OrderBy(x => x.Order).ToList();
            Assert.Equal(levels[1].Id, stored[0].Id);
            Assert.Equal(levels[0].Id, stored[1].Id);
        });
    }
}
