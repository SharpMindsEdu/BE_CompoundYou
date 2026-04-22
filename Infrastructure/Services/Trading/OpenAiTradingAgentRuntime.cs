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
        var payload = BuildPayload(request);

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

        var text = ExtractOutputText(doc.RootElement);
        var structured = ExtractStructuredOutput(doc.RootElement, text);

        return new TradingAgentRuntimeResponse(text, structured);
    }

    private object BuildPayload(TradingAgentRuntimeRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.Value.Model,
            ["temperature"] = _options.Value.Temperature,
            ["input"] = new object[]
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

        if (request.JsonSchema is null)
        {
            return payload;
        }

        var schemaElement = JsonDocument.Parse(request.JsonSchema.Schema).RootElement.Clone();
        payload["text"] = new Dictionary<string, object?>
        {
            ["format"] = new Dictionary<string, object?>
            {
                ["type"] = "json_schema",
                ["name"] = request.JsonSchema.Name,
                ["schema"] = schemaElement,
                ["strict"] = request.JsonSchema.Strict,
            },
        };

        return payload;
    }

    private static string ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText))
        {
            if (outputText.ValueKind == JsonValueKind.String)
            {
                return outputText.GetString() ?? string.Empty;
            }

            if (outputText.ValueKind == JsonValueKind.Array)
            {
                var fragments = outputText
                    .EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x));
                return string.Join(Environment.NewLine, fragments!);
            }
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var textParts = new List<string>();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                var type = contentItem.TryGetProperty("type", out var typeValue)
                    ? typeValue.GetString()
                    : null;
                if (!"output_text".Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (
                    contentItem.TryGetProperty("text", out var textValue)
                    && textValue.ValueKind == JsonValueKind.String
                )
                {
                    var text = textValue.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        textParts.Add(text);
                    }
                }
            }
        }

        return string.Join(Environment.NewLine, textParts);
    }

    private static IReadOnlyDictionary<string, object?> ExtractStructuredOutput(
        JsonElement root,
        string text
    )
    {
        if (TryExtractStructuredOutputFromItems(root, out var structured))
        {
            return structured;
        }

        if (TryParseJsonObject(text, out structured))
        {
            return structured;
        }

        return new Dictionary<string, object?>();
    }

    private static bool TryExtractStructuredOutputFromItems(
        JsonElement root,
        out IReadOnlyDictionary<string, object?> structured
    )
    {
        structured = new Dictionary<string, object?>();

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                var type = contentItem.TryGetProperty("type", out var typeValue)
                    ? typeValue.GetString()
                    : null;
                if (!"output_json".Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (
                    contentItem.TryGetProperty("json", out var jsonValue)
                    && jsonValue.ValueKind == JsonValueKind.Object
                )
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                        jsonValue.GetRawText()
                    );
                    if (parsed is not null)
                    {
                        structured = parsed;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryParseJsonObject(
        string input,
        out IReadOnlyDictionary<string, object?> structured
    )
    {
        structured = new Dictionary<string, object?>();
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (
            trimmed.StartsWith("```", StringComparison.Ordinal)
            && trimmed.EndsWith("```", StringComparison.Ordinal)
        )
        {
            trimmed = trimmed.Trim('`').Trim();
            if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[4..].Trim();
            }
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(trimmed);
            if (parsed is null || parsed.Count == 0)
            {
                return false;
            }

            structured = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
