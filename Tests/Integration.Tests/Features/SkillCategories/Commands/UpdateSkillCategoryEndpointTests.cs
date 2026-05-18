using Domain.Enums;
using Application.Features.SkillCategories.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.SkillCategories.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.SkillCategoryTests)]
public sealed class UpdateSkillCategoryEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateSkillCategory_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateSkillCategory.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task UpdateSkillCategory_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var category = await SeedSkillCategoryAsync(ctx.Tenant, cancellationToken: ct);
        var name = UniqueName("Updated Category");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/skill-categories/{id:long}", ("id", category.Id)),
            ctx.Token,
            new { Id = category.Id, Name = name, Description = "Updated", IsActive = true },
            ct
        );

        Assert.Equal(category.Id, GetRequiredLong(json, "id"));
        Assert.Equal(name, GetRequiredString(json, "name"));
    
    }
}
