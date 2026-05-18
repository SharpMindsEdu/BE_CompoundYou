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

    [Fact]
    public async Task GetPublicChatRooms_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var room = await SeedChatRoomAsync(name: UniqueName("Visible Room"), isPublic: true, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            WithQuery("api/chats/public", ("search", "Visible")),
            ctx.Token,
            cancellationToken: ct
        );

        AssertPageContainsId(json, room.Id);
    
    }
}
