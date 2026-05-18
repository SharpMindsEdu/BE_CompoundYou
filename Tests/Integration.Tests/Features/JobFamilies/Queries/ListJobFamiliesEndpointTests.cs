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
}
