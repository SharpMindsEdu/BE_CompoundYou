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

    public AuthService(HttpClient client, ISerializer serializer)
    {
        _client = client;
        _serializer = serializer;
    }

    public async Task<string?> RegisterAsync(string displayName, string? email, string? phone, CancellationToken ct = default)
    {
        var body = new RegisterUserCommand(displayName, email, phone);
        var content = new StringContent(_serializer.ToString(body), Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/api/users/register", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        var json = await response.Content.ReadAsStringAsync(ct);
        var token = _serializer.FromString<TokenDto>(json);
        return token.Token;
    }

    public async Task<string?> LoginAsync(string code, string? email, string? phone, CancellationToken ct = default)
    {
        var body = new LoginCommand(code, email, phone);
        var content = new StringContent(_serializer.ToString(body), Encoding.UTF8, "application/json");
        using var response = await _client.PutAsync("/api/users/login", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        var json = await response.Content.ReadAsStringAsync(ct);
        var token = _serializer.FromString<TokenDto>(json);
        return token.Token;
    }
}
