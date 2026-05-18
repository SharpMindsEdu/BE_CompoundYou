using Application.Features.Users.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Users.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class SearchUsersByNameEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SearchUsersByName_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            SearchUsersByName.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
