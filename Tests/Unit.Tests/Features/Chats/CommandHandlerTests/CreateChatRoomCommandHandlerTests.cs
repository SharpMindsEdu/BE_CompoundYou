using Application.Features.Chats.Commands;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Chats.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
public class CreateChatRoomCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateChatRoom_Public_ShouldCreateRoomAndAdmin()
    {
        var user = new User { DisplayName = "Creator" };
        PersistWithDatabase(db => db.Add(user));

        var cmd = new CreateChatRoom.CreateChatRoomCommand(user.Id, "Room", true, null);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var room = db.Set<ChatRoom>().Single();
            Assert.Equal("Room", room.Name);
            var admin = db.Set<ChatRoomUser>().Single(x => x.UserId == user.Id);
            Assert.True(admin.IsAdmin);
        });
    }

    [Fact]
    public async Task CreateChatRoom_PrivateTwoUsers_ShouldMarkDirect()
    {
        var user1 = new User { DisplayName = "A" };
        var user2 = new User { DisplayName = "B" };
        PersistWithDatabase(db => db.AddRange(user1, user2));

        var cmd = new CreateChatRoom.CreateChatRoomCommand(user1.Id, "DM", false, new() { user1.Id, user2.Id });
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Data!.IsDirect);
    }

    [Fact]
    public async Task CreateChatRoom_WithEmptyName_ShouldThrowValidation()
    {
        var cmd = new CreateChatRoom.CreateChatRoomCommand(1, "", true, null);
        await Assert.ThrowsAsync<ValidationException>(() => Send(cmd, TestContext.Current.CancellationToken));
    }
}
