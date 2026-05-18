using Domain.Enums;
using Application.Features.RoleProfileSkillRequirements.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfileSkillRequirements.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileSkillRequirementTests)]
public sealed class BulkSetRoleProfileSkillRequirementsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task BulkSetRoleProfileSkillRequirements_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(BulkSetRoleProfileSkillRequirements.Endpoint, ("roleProfileId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task BulkSetRoleProfileSkillRequirements_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var role = await SeedRoleProfileAsync(ctx.Tenant, cancellationToken: ct);
        var skill = await SeedSkillAsync(ctx.Tenant, cancellationToken: ct);
        var level = await SeedSkillLevelAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/role-profiles/{roleProfileId:long}/skill-requirements", ("roleProfileId", role.Id)),
            ctx.Token,
            new
            {
                RoleProfileId = role.Id,
                Requirements = new[] { new { SkillId = skill.Id, RequiredSkillLevelId = level.Id, Weight = 1m } },
            },
            ct
        );

        Assert.NotEmpty(json.EnumerateArray());
        Assert.Equal(skill.Id, GetRequiredLong(json.EnumerateArray().First(), "skillId"));
    
    }
}
