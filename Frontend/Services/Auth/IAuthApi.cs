using Frontend.Models;
using Refit;

namespace Frontend.Services.Auth;

[Headers("Content-Type: application/json")]
public interface IAuthApi
{
    [Post("/api/users/register")]
    Task<TokenDto> Register(RegisterUserCommand command, CancellationToken ct = default);

    [Put("/api/users/login")]
    Task<TokenDto> Login(LoginCommand command, CancellationToken ct = default);
}
