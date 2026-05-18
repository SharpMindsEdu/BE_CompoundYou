using Domain.Enums;
using Application.Features.RoleProfiles.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfiles.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileTests)]
public sealed class ListRoleProfilesEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListRoleProfiles_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListRoleProfiles.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ListRoleProfiles_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var role = await SeedRoleProfileAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/role-profiles",
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, role.Id);
    
    }
}
