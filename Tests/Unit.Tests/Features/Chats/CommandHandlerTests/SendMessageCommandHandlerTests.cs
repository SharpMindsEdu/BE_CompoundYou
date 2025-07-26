using Application.Common;
using Application.Features.Chats.Commands;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Chats.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public class SendMessageCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task SendMessage_AsMember_SavesMessage()
    {
        var user = new User { DisplayName = "Member" };
        var room = new ChatRoom { Name = "Room", IsPublic = true };
        var membership = new ChatRoomUser { ChatRoom = room, User = user };
        PersistWithDatabase(db => db.Add(membership));

        var cmd = new SendMessage.SendMessageCommand(user.Id, room.Id, "hi");
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var msg = db.Set<ChatMessage>().Single();
            Assert.Equal("hi", msg.Content);
        });
    }

    [Fact]
    public async Task SendMessage_NotMember_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "NoMember" };
        var room = new ChatRoom { Name = "Room", IsPublic = true };
        PersistWithDatabase(db => db.AddRange(user, room));

        var cmd = new SendMessage.SendMessageCommand(user.Id, room.Id, "hi");
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SendMessage_EmptyContent_ShouldThrowValidation()
    {
        var cmd = new SendMessage.SendMessageCommand(1, 1, "");
        await Assert.ThrowsAsync<ValidationException>(() =>
            Send(cmd, TestContext.Current.CancellationToken)
        );
    }
}
