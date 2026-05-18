using Domain.Entities.Chat;
using Microsoft.EntityFrameworkCore;
using Application.Features.Chats.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Chats.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public sealed class PromoteUserEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task PromoteUser_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(PromoteUser.Endpoint, ("roomId", 1), ("userId", 2)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task PromoteUser_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var target = await SeedUserAsync(cancellationToken: ct);
        var room = await SeedChatRoomAsync(cancellationToken: ct);
        await SeedChatRoomUserAsync(room, ctx.User, isAdmin: true, cancellationToken: ct);
        await SeedChatRoomUserAsync(room, target, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            Route("api/chats/rooms/{roomId:long}/promote/{userId:long}", ("roomId", room.Id), ("userId", target.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.True(json.GetBoolean());
        await using var db = CreateDbContext();
        var member = await db.Set<ChatRoomUser>().SingleAsync(x => x.ChatRoomId == room.Id && x.UserId == target.Id, ct);
        Assert.True(member.IsAdmin);
    
    }
}
