using Application.Common;
using Application.Features.Chats.Commands;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Chats.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public class JoinChatRoomCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task JoinChatRoom_PublicRoom_AddsMembership()
    {
        var user = new User { DisplayName = "Joiner" };
        PersistWithDatabase(db => db.Add(user));
        var room = new ChatRoom { Name = "Pub", IsPublic = true };
        PersistWithDatabase(db => db.Add(room));

        var cmd = new JoinChatRoom.JoinChatRoomCommand(room.Id, user.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var member = db.Set<ChatRoomUser>().Single(x => x.UserId == user.Id);
            Assert.Equal(room.Id, member.ChatRoomId);
        });
    }

    [Fact]
    public async Task JoinChatRoom_PrivateRoom_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "Fail" };
        PersistWithDatabase(db => db.Add(user));
        var room = new ChatRoom { Name = "Priv", IsPublic = false };
        PersistWithDatabase(db => db.Add(room));

        var cmd = new JoinChatRoom.JoinChatRoomCommand(room.Id, user.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task JoinChatRoom_ExistingMembership_ShouldNotDuplicate()
    {
        var user = new User { DisplayName = "Exists" };
        var room = new ChatRoom { Name = "Pub", IsPublic = true };
        var membership = new ChatRoomUser { ChatRoom = room, User = user };
        PersistWithDatabase(db => db.Add(membership));

        var cmd = new JoinChatRoom.JoinChatRoomCommand(room.Id, user.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var count = db.Set<ChatRoomUser>()
                .Count(x => x.ChatRoomId == room.Id && x.UserId == user.Id);
            Assert.Equal(1, count);
        });
    }

    [Fact]
    public async Task JoinChatRoom_WithMissingUser_ShouldThrowValidation()
    {
        var cmd = new JoinChatRoom.JoinChatRoomCommand(1, null);
        await Assert.ThrowsAsync<ValidationException>(() =>
            Send(cmd, TestContext.Current.CancellationToken)
        );
    }
}
