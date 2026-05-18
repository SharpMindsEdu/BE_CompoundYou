using Domain.Enums;
using Application.Features.RoleProfiles.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfiles.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileTests)]
public sealed class CopyRoleProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CopyRoleProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(CopyRoleProfile.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CopyRoleProfile_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var role = await SeedRoleProfileAsync(ctx.Tenant, cancellationToken: ct);
        var name = UniqueName("Copied Role");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            Route("api/role-profiles/{id:long}/copy", ("id", role.Id)),
            ctx.Token,
            new { Id = role.Id, Name = name },
            ct
        );

        Assert.NotEqual(role.Id, GetRequiredLong(json, "id"));
        Assert.Equal(name, GetRequiredString(json, "name"));
    
    }
}
