using System.Threading;
using System.Threading.Tasks;
using Frontend.Models;
using Refit;

namespace Frontend.Services.Auth;

public class AuthService
{
    private readonly IAuthApi _api;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IAuthApi api, ILogger<AuthService> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<string?> RegisterAsync(string displayName, string? email, string? phone, CancellationToken ct = default)
    {
        var command = new RegisterUserCommand(displayName, email, phone);
        _logger.LogInformation("Sending register request for {DisplayName}", displayName);
        try
        {
            var token = await _api.Register(command, ct);
            _logger.LogInformation("User {DisplayName} registered", displayName);
            return token.Token;
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Register failed with status {StatusCode}", ex.StatusCode);
            return null;
        }
    }

    public async Task<string?> LoginAsync(string code, string? email, string? phone, CancellationToken ct = default)
    {
        var command = new LoginCommand(code, email, phone);
        _logger.LogInformation("Sending login request");
        try
        {
            var token = await _api.Login(command, ct);
            _logger.LogInformation("Login successful");
            return token.Token;
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Login failed with status {StatusCode}", ex.StatusCode);
            return null;
        }
    }
}
