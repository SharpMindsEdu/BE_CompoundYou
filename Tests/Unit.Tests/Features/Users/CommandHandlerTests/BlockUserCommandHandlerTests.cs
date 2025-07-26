using Application.Features.Users.Commands;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Users.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public class BlockUserCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task BlockUser_FirstTime_ShouldCreateBlock()
    {
        var user1 = new User { DisplayName = "Blocker" };
        var user2 = new User { DisplayName = "Target" };
        PersistWithDatabase(db => db.AddRange(user1, user2));

        var cmd = new BlockUser.BlockUserCommand(user2.Id, user1.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var exists = db.Set<UserBlock>().Any(x => x.UserId == user1.Id && x.BlockedUserId == user2.Id);
            Assert.True(exists);
        });
    }

    [Fact]
    public async Task BlockUser_AlreadyBlocked_ShouldNotDuplicate()
    {
        var user1 = new User { DisplayName = "Blocker2" };
        var user2 = new User { DisplayName = "Target2" };
        var block = new UserBlock { UserId = user1.Id, BlockedUserId = user2.Id };
        PersistWithDatabase(db => db.AddRange(user1, user2, block));

        var cmd = new BlockUser.BlockUserCommand(user2.Id, user1.Id);
        var result = await Send(cmd, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var count = db.Set<UserBlock>().Count(x => x.UserId == user1.Id && x.BlockedUserId == user2.Id);
            Assert.Equal(1, count);
        });
    }

    [Fact]
    public async Task BlockUser_WithInvalidUserId_ShouldThrowValidationException()
    {
        var cmd = new BlockUser.BlockUserCommand(0, null);
        await Assert.ThrowsAsync<ValidationException>(() => Send(cmd, TestContext.Current.CancellationToken));
    }
}
