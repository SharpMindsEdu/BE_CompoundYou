using Domain.Enums;
using Application.Features.RoleProfiles.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfiles.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileTests)]
public sealed class CreateRoleProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateRoleProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateRoleProfile.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CreateRoleProfile_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var family = await SeedJobFamilyAsync(ctx.Tenant, cancellationToken: ct);
        var level = await SeedCareerLevelAsync(ctx.Tenant, family, cancellationToken: ct);
        var name = UniqueName("Role Profile");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/role-profiles",
            ctx.Token,
            new { JobFamilyId = family.Id, CareerLevelId = level.Id, Name = name, Description = "Created" },
            ct
        );

        Assert.Equal(name, GetRequiredString(json, "name"));
        Assert.Equal(family.Id, GetRequiredLong(json, "jobFamilyId"));
    
    }
}
