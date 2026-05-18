using Domain.Enums;
using Application.Features.JobFamilies.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.JobFamilies.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.JobFamilyTests)]
public sealed class CreateJobFamilyEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateJobFamily_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateJobFamily.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CreateJobFamily_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var name = UniqueName("Job Family");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/job-families",
            ctx.Token,
            new { Name = name, Description = "People growth track" },
            ct
        );

        Assert.Equal(name, GetRequiredString(json, "name"));
    
    }
}
