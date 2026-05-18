using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Application.Features.Users.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Users.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class BlockUserEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task BlockUser_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            Route(BlockUser.Endpoint, ("userId", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task BlockUser_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(cancellationToken: ct);
        var target = await SeedUserAsync(cancellationToken: ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            Route("api/users/{userId:long}/block", ("userId", target.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.True(json.GetBoolean());
        await using var db = CreateDbContext();
        Assert.True(await db.Set<UserBlock>().AnyAsync(x => x.UserId == ctx.User.Id && x.BlockedUserId == target.Id, ct));
    
    }
}
