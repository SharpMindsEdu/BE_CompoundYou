using Application.Features.Chats.Queries;
using Domain.Entities;
using Domain.Entities.Chat;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Chats.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public class GetPublicChatRoomsQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetPublicChatRooms_WithSearch_ReturnsMatching()
    {
        var room1 = new ChatRoom { Name = "Dogs", IsPublic = true };
        var room2 = new ChatRoom { Name = "Cats", IsPublic = true };
        var privateRoom = new ChatRoom { Name = "Secret", IsPublic = false };
        PersistWithDatabase(db => db.AddRange(room1, room2, privateRoom));

        var query = new GetPublicChatRooms.GetPublicChatRoomsQuery("Dog", 1, 10);
        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!.Items);
        Assert.Equal(room1.Name, result.Data.Items.First().Name);
    }
}
