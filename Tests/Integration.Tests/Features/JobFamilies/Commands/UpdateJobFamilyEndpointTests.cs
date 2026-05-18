using Domain.Enums;
using Application.Features.JobFamilies.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.JobFamilies.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.JobFamilyTests)]
public sealed class UpdateJobFamilyEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateJobFamily_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateJobFamily.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task UpdateJobFamily_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var family = await SeedJobFamilyAsync(ctx.Tenant, cancellationToken: ct);
        var name = UniqueName("Updated Family");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/job-families/{id:long}", ("id", family.Id)),
            ctx.Token,
            new { Id = family.Id, Name = name, Description = "Updated", IsActive = true },
            ct
        );

        Assert.Equal(family.Id, GetRequiredLong(json, "id"));
        Assert.Equal(name, GetRequiredString(json, "name"));
    
    }
}
