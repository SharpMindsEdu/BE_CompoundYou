using Application.Features.CareerLevels.Commands;
using Application.Features.RoleProfiles.Commands;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.CareerFrameworks.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.CareerTests)]
public sealed class CreateCareerLevelCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateCareerLevel_WithSubLevelOrder_ShouldPersistDecimalOrder()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        var family = new JobFamily { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(family));

        var result = await Send(
            new CreateCareerLevel.CreateCareerLevelCommand(family.Id, 1.1m, "IC1.1", null),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(1.1m, result.Data!.Order);

        WithDatabase(db =>
        {
            var level = db.Set<CareerLevel>().Single(x => x.JobFamilyId == family.Id);
            Assert.Equal(1.1m, level.Order);
        });
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.CareerTests)]
public sealed class CopyRoleProfileCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CopyRoleProfile_ShouldCopyRequirementsWithWeights()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);

        var family = new JobFamily { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(family));
        var careerLevel = new CareerLevel { JobFamilyId = family.Id, Order = 2.1m, Name = "Senior IC" };
        PersistWithDatabase(db => db.Add(careerLevel));

        var category = new SkillCategory { Name = "Engineering" };
        PersistWithDatabase(db => db.Add(category));
        var architecture = new Skill
        {
            SkillCategoryId = category.Id,
            Name = "Architecture",
            IsActive = true,
        };
        var communication = new Skill
        {
            SkillCategoryId = category.Id,
            Name = "Communication",
            IsActive = true,
        };
        PersistWithDatabase(db => db.AddRange(architecture, communication));

        var requiredLevel = new SkillLevel
        {
            Order = 4,
            Name = "Lead",
            PointsThreshold = 150,
        };
        PersistWithDatabase(db => db.Add(requiredLevel));

        var sourceRole = new RoleProfile
        {
            JobFamilyId = family.Id,
            CareerLevelId = careerLevel.Id,
            Name = "Senior Engineer",
            Description = "Owns delivery across a complex domain.",
        };
        PersistWithDatabase(db => db.Add(sourceRole));
        PersistWithDatabase(db =>
            db.AddRange(
                new RoleProfileSkillRequirement
                {
                    RoleProfileId = sourceRole.Id,
                    SkillId = architecture.Id,
                    RequiredSkillLevelId = requiredLevel.Id,
                    Weight = 1.5m,
                },
                new RoleProfileSkillRequirement
                {
                    RoleProfileId = sourceRole.Id,
                    SkillId = communication.Id,
                    RequiredSkillLevelId = requiredLevel.Id,
                    Weight = 2.25m,
                }
            )
        );

        var result = await Send(
            new CopyRoleProfile.CopyRoleProfileCommand(sourceRole.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("Senior Engineer Copy", result.Data!.Name);
        Assert.Equal(2.1m, result.Data.CareerLevelOrder);

        WithDatabase(db =>
        {
            var copy = db.Set<RoleProfile>().Single(x => x.Id == result.Data.Id);
            Assert.Equal(sourceRole.JobFamilyId, copy.JobFamilyId);
            Assert.Equal(sourceRole.CareerLevelId, copy.CareerLevelId);
            Assert.Equal(sourceRole.Description, copy.Description);

            var copiedRequirements = db.Set<RoleProfileSkillRequirement>()
                .Where(x => x.RoleProfileId == copy.Id)
                .OrderBy(x => x.SkillId)
                .ToList();

            Assert.Equal(2, copiedRequirements.Count);
            Assert.Equal(new[] { architecture.Id, communication.Id }, copiedRequirements.Select(x => x.SkillId));
            Assert.Equal(new[] { 1.5m, 2.25m }, copiedRequirements.Select(x => x.Weight));
            Assert.All(copiedRequirements, x => Assert.Equal(requiredLevel.Id, x.RequiredSkillLevelId));
        });
    }
}
