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

    [Fact]
    public async Task GetExceptionLogById_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var log = await SeedExceptionLogAsync(message: "Seeded details", cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/diagnostics/exceptions/{id:long}", ("id", log.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.Equal(log.Id, GetRequiredLong(json, "id"));
        Assert.Equal("Seeded details", GetRequiredString(json, "message"));
    
    }
}
