using Domain.Enums;
using Application.Features.CareerLevels.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerLevels.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerLevelTests)]
public sealed class ListCareerLevelsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListCareerLevels_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(ListCareerLevels.Endpoint, ("jobFamilyId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ListCareerLevels_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var family = await SeedJobFamilyAsync(ctx.Tenant, cancellationToken: ct);
        var level = await SeedCareerLevelAsync(ctx.Tenant, family, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/job-families/{jobFamilyId:long}/levels", ("jobFamilyId", family.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, level.Id);
    
    }
}
