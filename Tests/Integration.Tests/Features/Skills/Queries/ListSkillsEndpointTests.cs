using Domain.Enums;
using Application.Features.Skills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Skills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class ListSkillsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListSkills_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListSkills.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ListSkills_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var skill = await SeedSkillAsync(ctx.Tenant, cancellationToken: ct);
        await SeedSkillLevelAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/skills",
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, skill.Id);
    
    }
}
