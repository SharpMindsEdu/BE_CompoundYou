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
}
