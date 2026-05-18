using Application.Features.Users.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Users.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class BlockUserEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task BlockUser_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(BlockUser.Endpoint, ("userId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
