using Application.Features.Chats.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Chats.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public sealed class JoinChatRoomEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task JoinChatRoom_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(JoinChatRoom.Endpoint, ("roomId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
