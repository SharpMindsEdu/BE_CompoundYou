using Domain.Enums;
using Application.Features.TeamSkillRequirements.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.TeamSkillRequirements.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamSkillRequirementTests)]
public sealed class BulkSetTeamSkillRequirementsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task BulkSetTeamSkillRequirements_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(BulkSetTeamSkillRequirements.Endpoint, ("teamId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task BulkSetTeamSkillRequirements_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Manager, cancellationToken: ct);
        var team = await SeedTeamAsync(ctx.Tenant, cancellationToken: ct);
        var skill = await SeedSkillAsync(ctx.Tenant, cancellationToken: ct);
        var level = await SeedSkillLevelAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/teams/{teamId:long}/skill-requirements", ("teamId", team.Id)),
            ctx.Token,
            new
            {
                TeamId = team.Id,
                Requirements = new[] { new { SkillId = skill.Id, RequiredSkillLevelId = level.Id, Weight = 1 } },
            },
            ct
        );

        Assert.NotEmpty(json.EnumerateArray());
        Assert.Equal(skill.Id, GetRequiredLong(json.EnumerateArray().First(), "skillId"));
    
    }
}
