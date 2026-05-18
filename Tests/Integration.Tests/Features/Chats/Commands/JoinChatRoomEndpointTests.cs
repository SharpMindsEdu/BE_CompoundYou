using Domain.Entities.Chat;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task JoinChatRoom_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var room = await SeedChatRoomAsync(isPublic: true, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            Route("api/chats/rooms/{roomId:long}/join", ("roomId", room.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.Equal(room.Id, GetRequiredLong(json, "id"));
        await using var db = CreateDbContext();
        Assert.True(await db.Set<ChatRoomUser>().AnyAsync(x => x.ChatRoomId == room.Id && x.UserId == ctx.User.Id, ct));
    
    }
}
