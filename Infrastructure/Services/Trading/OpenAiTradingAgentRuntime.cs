using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Features.Trading.Automation;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public sealed class OpenAiTradingAgentRuntime : ITradingAgentRuntime
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<OpenAiTradingOptions> _options;

    public OpenAiTradingAgentRuntime(HttpClient httpClient, IOptions<OpenAiTradingOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<TradingAgentRuntimeResponse> RunAsync(
        TradingAgentRuntimeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var payload = new
        {
            model = _options.Value.Model,
            temperature = _options.Value.Temperature,
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = new object[] { new { type = "input_text", text = request.SystemPrompt } },
                },
                new
                {
                    role = "user",
                    content = new object[] { new { type = "input_text", text = request.UserPrompt } },
                },
            },
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.Value.BaseUrl.TrimEnd('/')}/v1/responses"
        );
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _options.Value.ApiKey
        );
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var text = doc.RootElement.TryGetProperty("output_text", out var outputText)
            ? outputText.GetString() ?? string.Empty
            : string.Empty;

        return new TradingAgentRuntimeResponse(text, new Dictionary<string, object?>());
    }
}
