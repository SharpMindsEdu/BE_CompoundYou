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

    [Fact]
    public async Task GetExceptionLogsSummary_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        await SeedExceptionLogAsync(exceptionType: "SummaryException", cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            WithQuery("api/diagnostics/exceptions/summary", ("exceptionType", "SummaryException")),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.Equal(1, json.GetProperty("totalExceptions").GetInt32());
        Assert.Equal(1, json.GetProperty("distinctExceptionTypes").GetInt32());
    
    }
}
