using Application.Features.Tenants.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Tenants.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class CreateTenantEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateTenant_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateTenant.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CreateTenant_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var admin = await CreatePlatformAdminContextAsync(ct);
        var owner = await SeedUserAsync(cancellationToken: ct);
        var slug = UniqueSlug("tenant-green");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/tenants",
            admin.Token,
            new { Name = UniqueName("Tenant"), Slug = slug, Plan = "growth", OwnerUserId = (long?)owner.Id },
            ct
        );

        Assert.Equal(slug, GetRequiredString(json, "slug"));
    
    }
}
