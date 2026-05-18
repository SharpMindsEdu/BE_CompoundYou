using Application.Features.Chats.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Chats.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public sealed class CreateChatRoomEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateChatRoom_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateChatRoom.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CreateChatRoom_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var other = await SeedUserAsync(cancellationToken: ct);
        var name = UniqueName("Public Room");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/chats/rooms",
            ctx.Token,
            new { UserId = (long?)null, Name = name, IsPublic = true, UserIds = new[] { other.Id } },
            ct
        );

        Assert.Equal(name, GetRequiredString(json, "name"));
        Assert.True(json.GetProperty("isPublic").GetBoolean());
    
    }
}
