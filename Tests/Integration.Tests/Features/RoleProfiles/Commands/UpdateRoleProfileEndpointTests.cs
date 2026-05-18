using Domain.Enums;
using Application.Features.RoleProfiles.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfiles.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileTests)]
public sealed class UpdateRoleProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateRoleProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateRoleProfile.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task UpdateRoleProfile_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var role = await SeedRoleProfileAsync(ctx.Tenant, cancellationToken: ct);
        var family = await SeedJobFamilyAsync(ctx.Tenant, cancellationToken: ct);
        var level = await SeedCareerLevelAsync(ctx.Tenant, family, cancellationToken: ct);
        var name = UniqueName("Updated Role");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/role-profiles/{id:long}", ("id", role.Id)),
            ctx.Token,
            new
            {
                Id = role.Id,
                JobFamilyId = family.Id,
                CareerLevelId = level.Id,
                Name = name,
                Description = "Updated",
                IsActive = true,
            },
            ct
        );

        Assert.Equal(role.Id, GetRequiredLong(json, "id"));
        Assert.Equal(name, GetRequiredString(json, "name"));
    
    }
}
