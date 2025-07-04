using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frontend.Models;

namespace Frontend.Services.Auth;

public class AuthService
{
    private readonly HttpClient _client;
    private readonly ISerializer _serializer;
    private readonly ILogger<AuthService> _logger;

    public AuthService(HttpClient client, ISerializer serializer, ILogger<AuthService> logger)
    {
        _client = client;
        _serializer = serializer;
        _logger = logger;
    }

    public async Task<string?> RegisterAsync(string displayName, string? email, string? phone, CancellationToken ct = default)
    {
        var body = new RegisterUserCommand(displayName, email, phone);
        var content = new StringContent(_serializer.ToString(body), Encoding.UTF8, "application/json");
        _logger.LogInformation("Sending register request for {DisplayName}", displayName);
        using var response = await _client.PostAsync("/api/users/register", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Register failed with status {StatusCode}", response.StatusCode);
            return null;
        }
        var json = await response.Content.ReadAsStringAsync(ct);
        var token = _serializer.FromString<TokenDto>(json);
        _logger.LogInformation("User {DisplayName} registered", displayName);
        return token.Token;
    }

    public async Task<string?> LoginAsync(string code, string? email, string? phone, CancellationToken ct = default)
    {
        var body = new LoginCommand(code, email, phone);
        var content = new StringContent(_serializer.ToString(body), Encoding.UTF8, "application/json");
        _logger.LogInformation("Sending login request");
        using var response = await _client.PutAsync("/api/users/login", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Login failed with status {StatusCode}", response.StatusCode);
            return null;
        }
        var json = await response.Content.ReadAsStringAsync(ct);
        var token = _serializer.FromString<TokenDto>(json);
        _logger.LogInformation("Login successful");
        return token.Token;
    }
}
