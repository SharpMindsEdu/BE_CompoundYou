using System.Text.Json;
using Application.Features.Trading.Automation;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace Infrastructure.Services.Trading;

public sealed class OpenAiTradingAgentRuntime : ITradingAgentRuntime
{
    private readonly IOptions<OpenAiTradingOptions> _options;

    public OpenAiTradingAgentRuntime(IOptions<OpenAiTradingOptions> options)
    {
        _options = options;
    }

    public async Task<TradingAgentRuntimeResponse> RunAsync(
        TradingAgentRuntimeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var sdkOptions = BuildCreateResponseOptions(request, _options.Value);
        var responseClient = BuildResponseClient(_options.Value);
        var result = await responseClient.CreateResponseAsync(sdkOptions, cancellationToken);

        using var stream = result.GetRawResponse().Content.ToStream();
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var text = result.Value.GetOutputText();
        if (string.IsNullOrWhiteSpace(text))
        {
            text = ExtractOutputText(doc.RootElement);
        }

        var structured = ExtractStructuredOutput(doc.RootElement, text);

        return new TradingAgentRuntimeResponse(text, structured);
    }

    private static CreateResponseOptions BuildCreateResponseOptions(
        TradingAgentRuntimeRequest request,
        OpenAiTradingOptions options
    )
    {
        var responseOptions = new CreateResponseOptions
        {
            Model = options.Model,
            Temperature = (float)options.Temperature,
        };
        responseOptions.InputItems.Add(
            ResponseItem.CreateSystemMessageItem(BuildSystemPrompt(request.SystemPrompt, options))
        );
        responseOptions.InputItems.Add(ResponseItem.CreateUserMessageItem(request.UserPrompt));

        if (request.Metadata is { Count: > 0 })
        {
            foreach (var (key, value) in request.Metadata)
            {
                responseOptions.Metadata[key] = value;
            }
        }

        if (options.UseAlpacaMcpServer && !string.IsNullOrWhiteSpace(options.AlpacaMcpServerUrl))
        {
            responseOptions.Tools.Add(BuildAlpacaMcpTool(options));
        }

        if (request.JsonSchema is not null)
        {
            responseOptions.TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: request.JsonSchema.Name,
                    jsonSchema: BinaryData.FromString(request.JsonSchema.Schema),
                    jsonSchemaIsStrict: request.JsonSchema.Strict
                ),
            };
        }

        return responseOptions;
    }

    private static McpTool BuildAlpacaMcpTool(OpenAiTradingOptions options)
    {
        var label = string.IsNullOrWhiteSpace(options.AlpacaMcpServerLabel)
            ? "alpaca"
            : options.AlpacaMcpServerLabel.Trim();
        var description = string.IsNullOrWhiteSpace(options.AlpacaMcpServerDescription)
            ? null
            : options.AlpacaMcpServerDescription.Trim();
        var authorization = NormalizeAuthorizationToken(options.AlpacaMcpAuthorization);

        return ResponseTool.CreateMcpTool(
            serverLabel: label,
            serverUri: new Uri(options.AlpacaMcpServerUrl.Trim(), UriKind.Absolute),
            authorizationToken: authorization,
            serverDescription: description,
            toolCallApprovalPolicy: BuildMcpToolCallApprovalPolicy(options.AlpacaMcpRequireApproval)
        );
    }

    private static McpToolCallApprovalPolicy BuildMcpToolCallApprovalPolicy(string? approvalPolicy)
    {
        var normalized = string.IsNullOrWhiteSpace(approvalPolicy)
            ? "never"
            : approvalPolicy.Trim();

        return normalized.Equals("always", StringComparison.OrdinalIgnoreCase)
            ? GlobalMcpToolCallApprovalPolicy.AlwaysRequireApproval
            : normalized.Equals("never", StringComparison.OrdinalIgnoreCase)
                ? GlobalMcpToolCallApprovalPolicy.NeverRequireApproval
                : new GlobalMcpToolCallApprovalPolicy(normalized);
    }

    private static string? NormalizeAuthorizationToken(string? authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        var trimmed = authorization.Trim();
        return trimmed.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[bearerPrefix.Length..].Trim()
            : trimmed;
    }

    private static string BuildSystemPrompt(string basePrompt, OpenAiTradingOptions options)
    {
        if (!options.UseAlpacaMcpServer || string.IsNullOrWhiteSpace(options.AlpacaMcpServerUrl))
        {
            return basePrompt;
        }

        var label = string.IsNullOrWhiteSpace(options.AlpacaMcpServerLabel)
            ? "alpaca"
            : options.AlpacaMcpServerLabel.Trim();
        var mcpInstruction =
            $"You have access to MCP server '{label}' at '{options.AlpacaMcpServerUrl.Trim()}'. Use this MCP server for Alpaca market and trading operations when relevant.";
        var optionsInstruction =
            "This workflow is options-first. Prefer option contracts over spot equity trades and verify option availability before recommending symbols.";
        return $"{basePrompt}\n\n{mcpInstruction}\n{optionsInstruction}";
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

    [Experimental("OPENAI001")]
    private static ResponsesClient BuildResponseClient(
        OpenAiTradingOptions options
    )
    {
        var endpoint = NormalizeEndpoint(options.BaseUrl);
        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(endpoint, UriKind.Absolute) };
        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(options.ApiKey),
            clientOptions
        );

        return openAiClient.GetResponsesClient();
    }

    private static string NormalizeEndpoint(string? baseUrl)
    {
        var endpoint = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com"
            : baseUrl.Trim();
        endpoint = endpoint.TrimEnd('/');

        if (!endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = $"{endpoint}/v1";
        }

        return endpoint;
    }
}

#pragma warning restore OPENAI001
