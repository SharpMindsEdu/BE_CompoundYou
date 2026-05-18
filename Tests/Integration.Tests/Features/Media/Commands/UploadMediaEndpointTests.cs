using Application.Features.Media.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Media.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.MediaTests)]
public sealed class UploadMediaEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UploadMedia_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            UploadMedia.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
