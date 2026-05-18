using Application.Features.EmployeeSkills.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class RejectAssessmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task RejectAssessment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(RejectAssessment.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }
}
