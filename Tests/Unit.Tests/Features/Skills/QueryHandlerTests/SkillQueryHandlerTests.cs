using Application.Features.Skills.Queries;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Skills.QueryHandlerTests;

public abstract class SkillQueryHandlerTestBase(
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
public sealed class ListSkillsQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : SkillQueryHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ListSkills_ShouldIncludeSkillLevelsOrderedByLevelOrder()
    {
        SeedSkillWithLevels();

        var result = await Send(
            new ListSkills.ListSkillsQuery(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        var skill = Assert.Single(result.Data!);
        Assert.Equal(new[] { "Beginner", "Advanced" }, skill.SkillLevels.Select(x => x.Name));
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class SearchSkillsQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : SkillQueryHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task SearchSkills_ShouldMatchNameAndIgnoreInactiveSkills()
    {
        var (category, _) = SeedSkillWithLevels();
        PersistWithDatabase(db =>
            db.Add(
                new Skill
                {
                    SkillCategoryId = category.Id,
                    Name = "Rust",
                    Description = "Systems",
                    IsActive = false,
                }
            )
        );

        var result = await Send(
            new SearchSkills.SearchSkillsQuery("c#"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!);
        Assert.Equal("C#", result.Data![0].Name);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class GetSkillTreeQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : SkillQueryHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetSkillTree_ShouldReturnParentChildHierarchy()
    {
        var (category, parent) = SeedSkillWithLevels();
        PersistWithDatabase(db =>
            db.Add(
                new Skill
                {
                    SkillCategoryId = category.Id,
                    Name = "ASP.NET",
                    ParentSkillId = parent.Id,
                    IsActive = true,
                }
            )
        );

        var result = await Send(
            new GetSkillTree.GetSkillTreeQuery(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        var root = Assert.Single(result.Data!);
        Assert.Equal("C#", root.Name);
        Assert.Single(root.Children);
        Assert.Equal("ASP.NET", root.Children[0].Name);
    }
}
