using Domain.Enums;
using Application.Features.Teams.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Teams.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TeamTests)]
public sealed class SetTeamManagerEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SetTeamManager_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(SetTeamManager.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task SetTeamManager_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var manager = await SeedEmployeeAsync(ctx.Tenant, cancellationToken: ct);
        var team = await SeedTeamAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/teams/{id:long}/manager", ("id", team.Id)),
            ctx.Token,
            new { ManagerEmployeeId = manager.Id },
            ct
        );

        Assert.Equal(manager.Id, GetRequiredLong(json, "managerEmployeeId"));
    
    }
}
