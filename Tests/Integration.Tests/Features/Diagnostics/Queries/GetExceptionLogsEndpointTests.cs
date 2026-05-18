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

    [Fact]
    public async Task GetExceptionLogs_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var log = await SeedExceptionLogAsync(exceptionType: "GreenPathException", cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            WithQuery("api/diagnostics/exceptions", ("exceptionType", "GreenPathException")),
            ctx.Token,
            cancellationToken: ct
        );

        AssertPageContainsId(json, log.Id);
    
    }
}
