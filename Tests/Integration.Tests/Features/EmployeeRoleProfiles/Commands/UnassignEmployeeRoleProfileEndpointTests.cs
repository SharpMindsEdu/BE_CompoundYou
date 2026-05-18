using Application.Features.EmployeeRoleProfiles.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeRoleProfiles.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeRoleProfileTests)]
public sealed class UnassignEmployeeRoleProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UnassignEmployeeRoleProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Delete,
            Route(UnassignEmployeeRoleProfile.Endpoint, ("employeeId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
