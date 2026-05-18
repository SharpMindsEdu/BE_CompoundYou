using Domain.Enums;
using Application.Features.SkillCategories.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.SkillCategories.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillCategoryTests)]
public sealed class ListSkillCategoriesEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task ListSkillCategories_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            ListSkillCategories.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task ListSkillCategories_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        var category = await SeedSkillCategoryAsync(ctx.Tenant, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/skill-categories",
            ctx.Token,
            cancellationToken: ct
        );

        AssertArrayContainsId(json, category.Id);
    
    }
}
