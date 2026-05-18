using Application.Features.Chats.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Chats.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public sealed class GetPublicChatRoomsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetPublicChatRooms_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetPublicChatRooms.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
