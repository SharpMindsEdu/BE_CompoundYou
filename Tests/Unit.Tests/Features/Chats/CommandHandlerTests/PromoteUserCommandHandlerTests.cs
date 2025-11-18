using Application.Features.Chats.Commands;
using Application.Shared;
using Domain.Entities;
using Domain.Entities.Chat;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Chats.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public class PromoteUserCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task PromoteUser_ByAdmin_SetsAdminFlag()
    {
        var admin = new User { DisplayName = "Admin" };
        var target = new User { DisplayName = "Member" };
        var room = new ChatRoom { Name = "R" };
        var adminMembership = new ChatRoomUser
        {
            ChatRoom = room,
            User = admin,
            IsAdmin = true,
        };
        var targetMembership = new ChatRoomUser { ChatRoom = room, User = target };
        PersistWithDatabase(db => db.AddRange(admin, target, adminMembership, targetMembership));

        var cmd = new PromoteUser.PromoteUserCommand(room.Id, target.Id, admin.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var m = db.Set<ChatRoomUser>()
                .Single(x => x.ChatRoomId == room.Id && x.UserId == target.Id);
            Assert.True(m.IsAdmin);
        });
    }

    [Fact]
    public async Task PromoteUser_UserMissing_ShouldReturnNotFound()
    {
        var admin = new User { DisplayName = "Admin" };
        var room = new ChatRoom { Name = "R" };
        var adminMembership = new ChatRoomUser
        {
            ChatRoom = room,
            User = admin,
            IsAdmin = true,
        };
        PersistWithDatabase(db => db.AddRange(admin, room, adminMembership));

        var cmd = new PromoteUser.PromoteUserCommand(room.Id, 999, admin.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
