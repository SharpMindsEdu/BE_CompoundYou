using Domain.Entities;
using Domain.Enums;
using Application.Features.Gdpr.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Gdpr.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.GdprTests)]
public sealed class RequestErasureEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task RequestErasure_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            RequestErasure.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task RequestErasure_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/gdpr/erase",
            ctx.Token,
            cancellationToken: ct
        );

        Assert.True(json.GetBoolean());
        await using var db = CreateDbContext();
        var user = await db.Set<User>().FindAsync([ctx.User.Id], ct);
        Assert.NotNull(user);
        Assert.Null(user.Email);
    
    }
}
