using Application.Features.Chats.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Chats.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.ChatTests)]
public sealed class InviteUserEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task InviteUser_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(InviteUser.Endpoint, ("roomId", 1), ("userId", 2)),
            TestContext.Current.CancellationToken
        );
    }
}
