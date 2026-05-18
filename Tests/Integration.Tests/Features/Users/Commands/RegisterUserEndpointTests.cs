using System.Net.Http.Json;
using Application.Features.Users.Commands;
using Application.Features.Users.DTOs;
using Application.Features.Users.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Users.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class RegisterUserEndpointTests(IntegrationTestStackFixture stack)
    : IntegrationTestBase(stack)
{
    [Fact]
    public async Task RegisterUser_WithValidEmail_ReturnsToken_AndAllowsGetCurrentUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var displayName = "Integration User";
        var email = UniqueEmail("register");

        var registerResponse = await Client.PostAsJsonAsync(
            RegisterUser.Endpoint,
            new RegisterUser.RegisterUserCommand(displayName, email, null),
            cancellationToken
        );

        var token = await ReadJsonAsync<TokenDto>(registerResponse, cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(token.Token));

        using var request = CreateAuthorizedRequest(HttpMethod.Get, GetUser.Endpoint, token.Token);
        var currentUserResponse = await Client.SendAsync(request, cancellationToken);

        var currentUser = await ReadJsonAsync<UserDto>(
            currentUserResponse,
            cancellationToken
        );
        Assert.Equal(displayName, currentUser.DisplayName);
        Assert.Equal(email, currentUser.Email);
    }
}
