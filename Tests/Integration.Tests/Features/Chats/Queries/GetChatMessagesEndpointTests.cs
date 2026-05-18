using Application.Features.Chats.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Chats.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public sealed class GetChatMessagesEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetChatMessages_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetChatMessages.Endpoint, ("roomId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
