using System.IO.Compression;
using Domain.Enums;
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

    [Fact]
    public async Task RequestDataExport_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        await SeedAuditLogEntryAsync(ctx.Tenant, ctx.User, cancellationToken: ct);

        using var response = await SendAuthorizedAsync(HttpMethod.Post, "api/gdpr/export", ctx.Token, cancellationToken: ct);
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, x => x.Name == "user.json");
    
    }
}
