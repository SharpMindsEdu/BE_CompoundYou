using Domain.Entities;
using Domain.Enums;
using Application.Features.SkillCategories.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.SkillCategories.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillCategoryTests)]
public sealed class CreateSkillCategoryEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateSkillCategory_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateSkillCategory.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CreateSkillCategory_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/skill-categories",
            ctx.Token,
            new { Name = UniqueName("Category"), Description = "Created", IsGlobal = false },
            ct
        );

        var id = json.GetInt64();
        await AssertEntityExistsAsync<SkillCategory>(id, ctx.Tenant, ct);
    
    }
}
