using Domain.Enums;
using Application.Features.TeamSkillRequirements.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.TeamSkillRequirements.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamSkillRequirementTests)]
public sealed class ListTeamSkillRequirementsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListTeamSkillRequirements_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(ListTeamSkillRequirements.Endpoint, ("teamId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ListTeamSkillRequirements_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var team = await SeedTeamAsync(ctx.Tenant, cancellationToken: ct);
        var skill = await SeedSkillAsync(ctx.Tenant, cancellationToken: ct);
        var level = await SeedSkillLevelAsync(ctx.Tenant, cancellationToken: ct);
        var requirement = await SeedTeamSkillRequirementAsync(ctx.Tenant, team, skill, level, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/teams/{teamId:long}/skill-requirements", ("teamId", team.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, requirement.Id);
    
    }
}
