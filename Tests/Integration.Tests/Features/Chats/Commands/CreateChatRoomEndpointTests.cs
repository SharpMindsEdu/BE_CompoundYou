using Application.Features.Chats.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Chats.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public sealed class CreateChatRoomEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateChatRoom_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateChatRoom.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
