using Application.Features.Diagnostics.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Diagnostics.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.DiagnosticsTests)]
public sealed class GetExceptionLogByIdEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetExceptionLogById_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(GetExceptionLogById.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
