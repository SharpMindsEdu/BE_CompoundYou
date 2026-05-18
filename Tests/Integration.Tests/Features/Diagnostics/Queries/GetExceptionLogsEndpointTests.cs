using Application.Features.Diagnostics.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Diagnostics.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.DiagnosticsTests)]
public sealed class GetExceptionLogsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetExceptionLogs_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetExceptionLogs.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
