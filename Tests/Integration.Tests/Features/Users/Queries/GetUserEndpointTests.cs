using Application.Features.Users.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Users.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class GetUserEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetUser_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetUser.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
