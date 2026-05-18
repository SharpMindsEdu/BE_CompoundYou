using Domain.Enums;
using Application.Features.Teams.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Teams.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class ListTeamsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListTeams_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListTeams.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ListTeams_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var team = await SeedTeamAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/teams",
            ctx.Token,
            cancellationToken: ct
        );

        AssertPageContainsId(json, team.Id);
    
    }
}
