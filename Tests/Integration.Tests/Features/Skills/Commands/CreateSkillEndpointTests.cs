using Domain.Entities;
using Domain.Enums;
using Application.Features.Skills.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Skills.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class CreateSkillEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateSkill_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateSkill.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CreateSkill_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var category = await SeedSkillCategoryAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/skills",
            ctx.Token,
            new
            {
                SkillCategoryId = category.Id,
                Name = UniqueName("Skill"),
                Description = "Created",
                ParentSkillId = (long?)null,
                IsGlobal = false,
            },
            ct
        );

        await AssertEntityExistsAsync<Skill>(json.GetInt64(), ctx.Tenant, ct);
    
    }
}
