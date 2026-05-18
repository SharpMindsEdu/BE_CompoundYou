using Domain.Enums;
using Application.Features.Skills.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Skills.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class UpdateSkillEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateSkill_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateSkill.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task UpdateSkill_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var category = await SeedSkillCategoryAsync(ctx.Tenant, cancellationToken: ct);
        var skill = await SeedSkillAsync(ctx.Tenant, category, cancellationToken: ct);
        var name = UniqueName("Updated Skill");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/skills/{id:long}", ("id", skill.Id)),
            ctx.Token,
            new
            {
                Id = skill.Id,
                SkillCategoryId = category.Id,
                Name = name,
                Description = "Updated",
                ParentSkillId = (long?)null,
                IsActive = true,
            },
            ct
        );

        Assert.Equal(skill.Id, GetRequiredLong(json, "id"));
        Assert.Equal(name, GetRequiredString(json, "name"));
    
    }
}
