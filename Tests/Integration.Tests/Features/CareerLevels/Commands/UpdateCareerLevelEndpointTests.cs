using Domain.Enums;
using Application.Features.CareerLevels.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerLevels.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerLevelTests)]
public sealed class UpdateCareerLevelEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateCareerLevel_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateCareerLevel.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task UpdateCareerLevel_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var family = await SeedJobFamilyAsync(ctx.Tenant, cancellationToken: ct);
        var level = await SeedCareerLevelAsync(ctx.Tenant, family, order: 1m, cancellationToken: ct);
        var name = UniqueName("Updated Level");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/career-levels/{id:long}", ("id", level.Id)),
            ctx.Token,
            new { Id = level.Id, Order = 2m, Name = name, Description = "Updated" },
            ct
        );

        Assert.Equal(level.Id, GetRequiredLong(json, "id"));
        Assert.Equal(name, GetRequiredString(json, "name"));
    
    }
}
