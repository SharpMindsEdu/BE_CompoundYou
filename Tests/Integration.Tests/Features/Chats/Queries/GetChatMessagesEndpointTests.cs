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

    [Fact]
    public async Task GetChatMessages_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var room = await SeedChatRoomAsync(cancellationToken: ct);
        await SeedChatRoomUserAsync(room, ctx.User, cancellationToken: ct);
        var message = await SeedChatMessageAsync(room, ctx.User, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/chats/rooms/{roomId:long}/messages", ("roomId", room.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        AssertPageContainsId(json, message.Id);
    
    }
}
