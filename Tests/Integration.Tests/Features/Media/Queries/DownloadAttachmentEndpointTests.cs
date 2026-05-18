using Application.Features.Media.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Media.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.MediaTests)]
public sealed class DownloadAttachmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task DownloadAttachment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(DownloadAttachment.Endpoint, ("messageId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
