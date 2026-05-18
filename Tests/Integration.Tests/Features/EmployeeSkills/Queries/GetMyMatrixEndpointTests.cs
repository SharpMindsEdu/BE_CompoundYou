using Application.Features.EmployeeSkills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class GetMyMatrixEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetMyMatrix_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetMyMatrix.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
