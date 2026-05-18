using System.Net.Http.Json;
using Application.Features.Users.Commands;
using Application.Features.Users.DTOs;
using Domain.Entities;
using Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Features.Users.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class LoginEndpointTests(IntegrationTestStackFixture stack)
    : IntegrationTestBase(stack)
{
    [Fact]
    public async Task Login_WithValidOtp_ReturnsToken_AndConsumesOtp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = UniqueEmail("login");
        var user = await SeedUserAsync(
            email: email,
            signInSecret: "123456",
            signInTries: 3,
            cancellationToken: cancellationToken
        );

        var response = await Client.PutAsJsonAsync(
            Login.Endpoint,
            new Login.LoginCommand("123456", email, null),
            cancellationToken
        );

        var token = await ReadJsonAsync<TokenDto>(response, cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(token.Token));

        await using var db = CreateDbContext();
        var storedUser = await db.Set<User>().SingleAsync(x => x.Id == user.Id, cancellationToken);
        Assert.Null(storedUser.SignInSecret);
    }
}
