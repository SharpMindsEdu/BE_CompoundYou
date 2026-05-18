using Application.Features.JobFamilies.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.JobFamilies.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.JobFamilyTests)]
public sealed class UpdateJobFamilyEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateJobFamily_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateJobFamily.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
