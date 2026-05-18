using Application.Features.Gdpr.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Gdpr.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.GdprTests)]
public sealed class RequestErasureEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task RequestErasure_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            RequestErasure.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
