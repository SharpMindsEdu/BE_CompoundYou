using Application.Features.EmployeeSkills.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class ValidateAssessmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ValidateAssessment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(ValidateAssessment.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
