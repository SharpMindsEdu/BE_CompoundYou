using Application.Features.EmployeeSkills.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.EmployeeSkills.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeSkillTests)]
public sealed class SubmitAssessmentEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SubmitAssessment_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            SubmitAssessment.Endpoint,
            TestContext.Current.CancellationToken
        );
    }
}
