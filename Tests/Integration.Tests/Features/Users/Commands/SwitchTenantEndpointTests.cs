using Domain.Enums;
using Application.Features.Users.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Users.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class SwitchTenantEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SwitchTenant_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            SwitchTenant.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task SwitchTenant_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var user = await SeedUserAsync(cancellationToken: ct);
        var tenantOne = await SeedTenantAsync(ownerUserId: user.Id, cancellationToken: ct);
        var tenantTwo = await SeedTenantAsync(ownerUserId: user.Id, cancellationToken: ct);
        await SeedTenantMembershipAsync(tenantOne, user, TenantRole.Employee, cancellationToken: ct);
        await SeedTenantMembershipAsync(tenantTwo, user, TenantRole.Manager, cancellationToken: ct);
        var pickerToken = await LoginAsync(user, ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/users/switch-tenant",
            pickerToken,
            new { TenantId = tenantTwo.Id },
            ct
        );

        Assert.False(string.IsNullOrWhiteSpace(GetRequiredString(json, "token")));
        Assert.False(json.GetProperty("requiresTenantSelection").GetBoolean());
    
    }
}
