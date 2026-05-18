using Domain.Enums;
using Application.Features.SkillLevelSystem;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.SkillLevelSystem;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillLevelSystemTests)]
public sealed class GetTenantSkillLevelSystemEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetTenantSkillLevelSystem_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            TenantSkillLevelSystem.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetTenantSkillLevelSystem_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var level = await SeedSkillLevelAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/skill-level-system",
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, level.Id);
    
    }
}
