using Domain.Enums;
using Application.Features.JobFamilies.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.JobFamilies.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.JobFamilyTests)]
public sealed class ListJobFamiliesEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListJobFamilies_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListJobFamilies.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ListJobFamilies_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var family = await SeedJobFamilyAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/job-families",
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, family.Id);
    
    }
}
