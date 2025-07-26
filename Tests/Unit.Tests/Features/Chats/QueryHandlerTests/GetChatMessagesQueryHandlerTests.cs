using Application.Common;
using Application.Features.Chats.Queries;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Chats.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
public class GetChatMessagesQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetChatMessages_AsMember_ReturnsMessages()
    {
        var user = new User { DisplayName = "Member" };
        var room = new ChatRoom { Name = "R" };
        var membership = new ChatRoomUser { ChatRoom = room, User = user };
        var message = new ChatMessage { ChatRoom = room, User = user, Content = "hey" };
        PersistWithDatabase(db => db.AddRange(membership, message));

        var query = new GetChatMessages.GetChatMessagesQuery(room.Id, user.Id, 1, 10);
        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!.Items);
        Assert.Equal("hey", result.Data.Items.First().Content);
    }

    [Fact]
    public async Task GetChatMessages_NotMember_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "NoMember" };
        var room = new ChatRoom { Name = "R" };
        PersistWithDatabase(db => db.AddRange(user, room));

        var query = new GetChatMessages.GetChatMessagesQuery(room.Id, user.Id, 1, 10);
        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
