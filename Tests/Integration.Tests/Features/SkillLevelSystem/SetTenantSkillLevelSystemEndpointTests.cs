using Domain.Enums;
using Application.Features.SkillLevelSystem;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.SkillLevelSystem;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillLevelSystemTests)]
public sealed class SetTenantSkillLevelSystemEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SetTenantSkillLevelSystem_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            TenantSkillLevelSystem.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task SetTenantSkillLevelSystem_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            "api/skill-level-system",
            ctx.Token,
            new
            {
                Levels = new[]
                {
                    new { Name = "Foundation", Description = "Can contribute", PointsThreshold = 0 },
                    new { Name = "Advanced", Description = "Can lead", PointsThreshold = 100 },
                },
            },
            ct
        );

        Assert.Equal(2, json.GetArrayLength());
        Assert.Equal("Foundation", GetRequiredString(json[0], "name"));
    
    }
}
