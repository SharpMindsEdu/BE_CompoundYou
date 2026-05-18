using Application.Features.Chats.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Chats.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public sealed class KickUserEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task KickUser_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(KickUser.Endpoint, ("roomId", 1), ("userId", 2)),
            TestContext.Current.CancellationToken
        );
    }
}
