using Application.Features.Audit.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Audit.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.AuditTests)]
public sealed class ListAuditEntriesEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListAuditEntries_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListAuditEntries.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
