using Application.Features.Chats.Commands;
using Application.Shared;
using Domain.Entities;
using Domain.Entities.Chat;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Chats.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public class InviteUserCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task InviteUser_ByAdmin_AddsUser()
    {
        var admin = new User { DisplayName = "Admin" };
        var target = new User { DisplayName = "Target" };
        var room = new ChatRoom { Name = "R", IsPublic = false };
        var adminMembership = new ChatRoomUser
        {
            ChatRoom = room,
            User = admin,
            IsAdmin = true,
        };
        PersistWithDatabase(db => db.AddRange(admin, target, adminMembership));

        var cmd = new InviteUser.InviteUserCommand(room.Id, target.Id, admin.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var exists = db.Set<ChatRoomUser>()
                .Any(x => x.ChatRoomId == room.Id && x.UserId == target.Id);
            Assert.True(exists);
        });
    }

    [Fact]
    public async Task InviteUser_ByNonAdmin_ShouldFail()
    {
        var user = new User { DisplayName = "User" };
        var target = new User { DisplayName = "T" };
        var room = new ChatRoom { Name = "R", IsPublic = false };
        var membership = new ChatRoomUser
        {
            ChatRoom = room,
            User = user,
            IsAdmin = false,
        };
        PersistWithDatabase(db => db.AddRange(user, target, membership));

        var cmd = new InviteUser.InviteUserCommand(room.Id, target.Id, user.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }
}
