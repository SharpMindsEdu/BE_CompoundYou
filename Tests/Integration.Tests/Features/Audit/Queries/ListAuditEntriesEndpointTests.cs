using Domain.Enums;
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

    [Fact]
    public async Task ListAuditEntries_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var entry = await SeedAuditLogEntryAsync(
            ctx.Tenant,
            ctx.User,
            entityType: "Department",
            cancellationToken: ct
        );

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            WithQuery("api/audit", ("entityType", "Department")),
            ctx.Token,
            cancellationToken: ct
        );

        AssertPageContainsId(json, entry.Id);
    
    }
}
