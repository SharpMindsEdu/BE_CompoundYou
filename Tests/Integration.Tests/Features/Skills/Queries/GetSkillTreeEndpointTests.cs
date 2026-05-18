using Domain.Enums;
using Application.Features.Skills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Skills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class GetSkillTreeEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetSkillTree_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetSkillTree.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetSkillTree_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var root = await SeedSkillAsync(ctx.Tenant, name: UniqueName("Root Skill"), cancellationToken: ct);
        var child = await SeedSkillAsync(ctx.Tenant, parentSkill: root, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/skills/tree",
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, root.Id);
        var rootJson = json.EnumerateArray().Single(x => GetRequiredLong(x, "id") == root.Id);
        AssertArrayContainsId(rootJson.GetProperty("children"), child.Id);
    
    }
}
