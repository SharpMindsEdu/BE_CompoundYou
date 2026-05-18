using Domain.Enums;
using Application.Features.Skills.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Skills.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public sealed class SearchSkillsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task SearchSkills_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            SearchSkills.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task SearchSkills_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var name = UniqueName("Searchable Skill");
        var skill = await SeedSkillAsync(ctx.Tenant, name: name, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            WithQuery("api/skills/search", ("term", name)),
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, skill.Id);
    
    }
}
