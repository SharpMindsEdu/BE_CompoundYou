using System.Net.Http.Json;
using Application.Features.Users.Commands;
using Domain.Entities;
using Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Features.Users.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class RequestLoginEndpointTests(IntegrationTestStackFixture stack)
    : IntegrationTestBase(stack)
{
    [Fact]
    public async Task RequestLogin_ForExistingUser_ReturnsOk_AndStoresOtp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = UniqueEmail("login-request");
        var user = await SeedUserAsync(email: email, cancellationToken: cancellationToken);

        var response = await Client.PutAsJsonAsync(
            RequestLogin.Endpoint,
            new RequestLogin.RequestLoginCommand(email, null),
            cancellationToken
        );

        var accepted = await ReadJsonAsync<bool>(response, cancellationToken);
        Assert.True(accepted);

        await using var db = CreateDbContext();
        var storedUser = await db.Set<User>().SingleAsync(x => x.Id == user.Id, cancellationToken);
        Assert.Equal("123456", storedUser.SignInSecret);
        Assert.Equal(3, storedUser.SignInTries);
    }
}
