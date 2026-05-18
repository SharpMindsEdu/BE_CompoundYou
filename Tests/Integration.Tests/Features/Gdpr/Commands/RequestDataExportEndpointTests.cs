using Application.Features.Gdpr.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Gdpr.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.GdprTests)]
public sealed class RequestDataExportEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task RequestDataExport_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            RequestDataExport.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
