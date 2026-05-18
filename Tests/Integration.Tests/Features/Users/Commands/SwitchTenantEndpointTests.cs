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
}
