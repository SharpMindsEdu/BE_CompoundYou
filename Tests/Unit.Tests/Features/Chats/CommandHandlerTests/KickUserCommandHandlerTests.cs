using Application.Features.Chats.Commands;
using Application.Shared;
using Domain.Entities;
using Domain.Entities.Chat;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Chats.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public class KickUserCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task KickUser_ByAdmin_RemovesUser()
    {
        var admin = new User { DisplayName = "Admin" };
        var target = new User { DisplayName = "Kick" };
        var room = new ChatRoom { Name = "R" };
        var adminMembership = new ChatRoomUser
        {
            ChatRoom = room,
            User = admin,
            IsAdmin = true,
        };
        var targetMembership = new ChatRoomUser { ChatRoom = room, User = target };
        PersistWithDatabase(db => db.AddRange(admin, target, adminMembership, targetMembership));

        var cmd = new KickUser.KickUserCommand(room.Id, target.Id, admin.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var exists = db.Set<ChatRoomUser>()
                .Any(x => x.ChatRoomId == room.Id && x.UserId == target.Id);
            Assert.False(exists);
        });
    }

    [Fact]
    public async Task KickUser_ByNonAdmin_ShouldFail()
    {
        var user = new User { DisplayName = "User" };
        var target = new User { DisplayName = "Kick" };
        var room = new ChatRoom { Name = "R" };
        var membership = new ChatRoomUser { ChatRoom = room, User = user };
        var targetMembership = new ChatRoomUser { ChatRoom = room, User = target };
        PersistWithDatabase(db => db.AddRange(user, target, membership, targetMembership));

        var cmd = new KickUser.KickUserCommand(room.Id, target.Id, user.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }
}
