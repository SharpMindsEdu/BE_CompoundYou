using Application.Features.EmployeeRoleProfiles.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeRoleProfiles.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeRoleProfileTests)]
public sealed class AssignEmployeeRoleProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task AssignEmployeeRoleProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(AssignEmployeeRoleProfile.Endpoint, ("employeeId", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
