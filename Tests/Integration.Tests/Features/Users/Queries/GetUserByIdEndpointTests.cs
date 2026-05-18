using Application.Features.Users.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Users.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class GetUserByIdEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetUserById_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetUserById.Endpoint, ("userId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
