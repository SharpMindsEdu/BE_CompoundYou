using Application.Features.Diagnostics.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Diagnostics.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.DiagnosticsTests)]
public sealed class GetExceptionLogsSummaryEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetExceptionLogsSummary_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetExceptionLogsSummary.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
