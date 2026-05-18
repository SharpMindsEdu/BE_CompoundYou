using Application.Features.Employees.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class GetMyProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetMyProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetMyProfile.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
