using Domain.Enums;
using Application.Features.CareerLevels.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerLevels.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerLevelTests)]
public sealed class CreateCareerLevelEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateCareerLevel_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(CreateCareerLevel.Endpoint, ("jobFamilyId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CreateCareerLevel_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var family = await SeedJobFamilyAsync(ctx.Tenant, cancellationToken: ct);
        var name = UniqueName("Career Level");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            Route("api/job-families/{jobFamilyId:long}/levels", ("jobFamilyId", family.Id)),
            ctx.Token,
            new { JobFamilyId = family.Id, Order = 1.25m, Name = name, Description = "Growth level" },
            ct
        );

        Assert.Equal(family.Id, GetRequiredLong(json, "jobFamilyId"));
        Assert.Equal(name, GetRequiredString(json, "name"));
    
    }
}
