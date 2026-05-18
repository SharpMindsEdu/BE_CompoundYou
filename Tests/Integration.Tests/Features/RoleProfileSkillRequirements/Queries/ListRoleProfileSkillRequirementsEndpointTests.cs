using Domain.Enums;
using Application.Features.RoleProfileSkillRequirements.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.RoleProfileSkillRequirements.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.RoleProfileSkillRequirementTests)]
public sealed class ListRoleProfileSkillRequirementsEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListRoleProfileSkillRequirements_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            Route(ListRoleProfileSkillRequirements.Endpoint, ("roleProfileId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ListRoleProfileSkillRequirements_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var role = await SeedRoleProfileAsync(ctx.Tenant, cancellationToken: ct);
        var skill = await SeedSkillAsync(ctx.Tenant, cancellationToken: ct);
        var level = await SeedSkillLevelAsync(ctx.Tenant, cancellationToken: ct);
        var requirement = await SeedRoleProfileSkillRequirementAsync(
            ctx.Tenant,
            role,
            skill,
            level,
            cancellationToken: ct
        );

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            Route("api/role-profiles/{roleProfileId:long}/skill-requirements", ("roleProfileId", role.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, requirement.Id);
    
    }
}
