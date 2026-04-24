using System.Text.Json;
using Application.Features.Trading.Automation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace Infrastructure.Services.Trading;

public sealed class OpenAiTradingAgentRuntime : ITradingAgentRuntime
{
    private const int MinimumPromptCharacterLimit = 512;
    private const string TruncationNotice = "\n\n...[truncated for model context window]...\n\n";

    private readonly ILogger<OpenAiTradingAgentRuntime> _logger;
    private readonly IOptions<OpenAiTradingOptions> _options;

    public OpenAiTradingAgentRuntime(
        IOptions<OpenAiTradingOptions> options,
        ILogger<OpenAiTradingAgentRuntime> logger
    )
    {
        _logger = logger;
        _options = options;
    }

    public async Task<TradingAgentRuntimeResponse> RunAsync(
        TradingAgentRuntimeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var options = _options.Value;
        var responseClient = BuildResponseClient(options);
        ClientResultException? contextExceededException = null;

        foreach (var attempt in BuildRuntimeAttempts(request, options))
        {
            try
            {
                return await RunSingleAttemptAsync(
                    responseClient,
                    options,
                    attempt.Model,
                    attempt.Request,
                    cancellationToken
                );
            }
            catch (ClientResultException ex) when (IsContextLengthExceeded(ex))
            {
                contextExceededException = ex;
                _logger.LogWarning(
                    ex,
                    "OpenAI context window exceeded for agent {AgentName} on attempt {AttemptName} (model={Model}, systemChars={SystemChars}, userChars={UserChars}).",
                    request.AgentName,
                    attempt.Name,
                    attempt.Model,
                    attempt.Request.SystemPrompt.Length,
                    attempt.Request.UserPrompt.Length
                );
            }
        }

        if (contextExceededException is not null)
        {
            throw contextExceededException;
        }

        throw new InvalidOperationException("OpenAI runtime attempt pipeline completed without a result.");
    }

    private static IReadOnlyCollection<RuntimeAttempt> BuildRuntimeAttempts(
        TradingAgentRuntimeRequest request,
        OpenAiTradingOptions options
    )
    {
        var attempts = new List<RuntimeAttempt>();
        var primaryModel = NormalizeModel(options.Model, "gpt-4.1-mini");
        var primaryRequest = ApplyPromptCharacterLimits(
            request,
            options.MaxSystemPromptCharacters,
            options.MaxUserPromptCharacters
        );
        attempts.Add(new RuntimeAttempt("primary", primaryModel, primaryRequest));

        if (options.EnableContextLengthRetry)
        {
            var reducedRequest = ApplyPromptCharacterLimits(
                primaryRequest,
                options.ContextRetrySystemPromptCharacters,
                options.ContextRetryUserPromptCharacters
            );

            if (!PromptsMatch(primaryRequest, reducedRequest))
            {
                attempts.Add(new RuntimeAttempt("reduced-context", primaryModel, reducedRequest));
            }
        }

        var fallbackModel = NormalizeModel(options.FallbackModel, string.Empty);
        if (
            !string.IsNullOrWhiteSpace(fallbackModel)
            && !fallbackModel.Equals(primaryModel, StringComparison.OrdinalIgnoreCase)
        )
        {
            var fallbackRequest = attempts[^1].Request;
            attempts.Add(new RuntimeAttempt("fallback-model", fallbackModel, fallbackRequest));
        }

        return attempts;
    }

    private static bool PromptsMatch(
        TradingAgentRuntimeRequest left,
        TradingAgentRuntimeRequest right
    )
    {
        return left.SystemPrompt.Equals(right.SystemPrompt, StringComparison.Ordinal)
            && left.UserPrompt.Equals(right.UserPrompt, StringComparison.Ordinal);
    }

    private static TradingAgentRuntimeRequest ApplyPromptCharacterLimits(
        TradingAgentRuntimeRequest request,
        int maxSystemPromptCharacters,
        int maxUserPromptCharacters
    )
    {
        var normalizedSystemLimit = NormalizePromptCharacterLimit(maxSystemPromptCharacters);
        var normalizedUserLimit = NormalizePromptCharacterLimit(maxUserPromptCharacters);

        return request with
        {
            SystemPrompt = TruncatePromptPreservingEdges(request.SystemPrompt, normalizedSystemLimit),
            UserPrompt = TruncatePromptPreservingEdges(request.UserPrompt, normalizedUserLimit),
        };
    }

    private static int NormalizePromptCharacterLimit(int configuredLimit)
    {
        if (configuredLimit <= 0)
        {
            return int.MaxValue;
        }

        return Math.Max(configuredLimit, MinimumPromptCharacterLimit);
    }

    private static string TruncatePromptPreservingEdges(string value, int maxCharacters)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters)
        {
            return value;
        }

        var payloadBudget = maxCharacters - TruncationNotice.Length;
        if (payloadBudget <= 2)
        {
            return value[..maxCharacters];
        }

        var prefixLength = Math.Max((int)Math.Floor(payloadBudget * 0.65), 1);
        var suffixLength = Math.Max(payloadBudget - prefixLength, 1);
        if (prefixLength + suffixLength > value.Length)
        {
            return value;
        }

        var prefix = value[..prefixLength];
        var suffix = value[^suffixLength..];
        return string.Concat(prefix, TruncationNotice, suffix);
    }

    private static bool IsContextLengthExceeded(ClientResultException exception)
    {
        return exception.Status == 400
            && exception.Message.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeModel(string? configuredModel, string fallback)
    {
        return string.IsNullOrWhiteSpace(configuredModel) ? fallback : configuredModel.Trim();
    }

    private static async Task<TradingAgentRuntimeResponse> RunSingleAttemptAsync(
        ResponsesClient responseClient,
        OpenAiTradingOptions options,
        string model,
        TradingAgentRuntimeRequest request,
        CancellationToken cancellationToken
    )
    {
        var sdkOptions = BuildCreateResponseOptions(request, options, model);
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
        OpenAiTradingOptions options,
        string model
    )
    {
        var responseOptions = new CreateResponseOptions
        {
            Model = model,
            TruncationMode = options.UseAutoTruncationMode
                ? ResponseTruncationMode.Auto
                : ResponseTruncationMode.Disabled,
            ReasoningOptions = new ResponseReasoningOptions()
            {
                ReasoningEffortLevel = ResponseReasoningEffortLevel.Medium
            }
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

        foreach (var tool in BuildMcpTools(options))
        {
            responseOptions.Tools.Add(tool);
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

    private static IEnumerable<McpTool> BuildMcpTools(OpenAiTradingOptions options)
    {
        var alpacaServer = ResolveAlpacaMcpServer(options);
        if (alpacaServer is not null)
        {
            yield return CreateMcpTool(alpacaServer);
        }

        var alphaVantageServer = ResolveAlphaVantageMcpServer(options);
        if (alphaVantageServer is not null)
        {
            yield return CreateMcpTool(alphaVantageServer);
        }
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
        var servers = ResolveMcpServers(options).ToArray();
        if (servers.Length == 0)
        {
            return basePrompt;
        }

        var mcpInstruction = string.Join(
            Environment.NewLine,
            servers.Select(BuildMcpInstruction)
        );
        var optionsInstruction =
            "This workflow is options-first. Prefer option contracts over spot equity trades and verify option availability before recommending symbols.";
        return $"{basePrompt}\n\n{mcpInstruction}\n{optionsInstruction}";
    }

    private static IEnumerable<ResolvedMcpServer> ResolveMcpServers(OpenAiTradingOptions options)
    {
        var alpacaServer = ResolveAlpacaMcpServer(options);
        if (alpacaServer is not null)
        {
            yield return alpacaServer;
        }

        var alphaVantageServer = ResolveAlphaVantageMcpServer(options);
        if (alphaVantageServer is not null)
        {
            yield return alphaVantageServer;
        }
    }

    private static ResolvedMcpServer? ResolveAlpacaMcpServer(OpenAiTradingOptions options)
    {
        if (!options.UseAlpacaMcpServer || string.IsNullOrWhiteSpace(options.AlpacaMcpServerUrl))
        {
            return null;
        }

        if (!TryResolveServerUri(options.AlpacaMcpServerUrl, null, out var serverUri))
        {
            return null;
        }

        var label = string.IsNullOrWhiteSpace(options.AlpacaMcpServerLabel)
            ? "alpaca"
            : options.AlpacaMcpServerLabel.Trim();
        var description = string.IsNullOrWhiteSpace(options.AlpacaMcpServerDescription)
            ? null
            : options.AlpacaMcpServerDescription.Trim();

        return new ResolvedMcpServer(
            label,
            serverUri,
            serverUri.ToString(),
            description,
            NormalizeAuthorizationToken(options.AlpacaMcpAuthorization),
            BuildMcpToolCallApprovalPolicy(options.AlpacaMcpRequireApproval),
            "Use this MCP server for Alpaca market and trading operations when relevant."
        );
    }

    private static ResolvedMcpServer? ResolveAlphaVantageMcpServer(OpenAiTradingOptions options)
    {
        if (
            !options.UseAlphaVantageMcpServer
            || string.IsNullOrWhiteSpace(options.AlphaVantageMcpServerUrl)
        )
        {
            return null;
        }

        var displayUri = RedactAlphaVantageApiKey(options.AlphaVantageMcpServerUrl);

        if (
            !TryResolveServerUri(
                options.AlphaVantageMcpServerUrl,
                options.AlphaVantageMcpApiKey,
                out var serverUri
            )
        )
        {
            return null;
        }

        var label = string.IsNullOrWhiteSpace(options.AlphaVantageMcpServerLabel)
            ? "alphavantage"
            : options.AlphaVantageMcpServerLabel.Trim();
        var description = string.IsNullOrWhiteSpace(options.AlphaVantageMcpServerDescription)
            ? null
            : options.AlphaVantageMcpServerDescription.Trim();

        return new ResolvedMcpServer(
            label,
            serverUri,
            displayUri,
            description,
            null,
            BuildMcpToolCallApprovalPolicy(options.AlpacaMcpRequireApproval),
            "Use this MCP server for Alpha Vantage NEWS_SENTIMENT and related market-data lookups when relevant."
        );
    }

    private static McpTool CreateMcpTool(ResolvedMcpServer server)
    {
        return ResponseTool.CreateMcpTool(
            serverLabel: server.Label,
            serverUri: server.Uri,
            authorizationToken: server.AuthorizationToken,
            serverDescription: server.Description,
            toolCallApprovalPolicy: server.ApprovalPolicy
        );
    }

    private static string BuildMcpInstruction(ResolvedMcpServer server)
    {
        return $"You have access to MCP server '{server.Label}' at '{server.DisplayUri}'. {server.PurposeInstruction}";
    }

    private static bool TryResolveServerUri(
        string serverUrl,
        string? loadFromConfigValue,
        out Uri? serverUri
    )
    {
        serverUri = null;

        var resolvedUrl = serverUrl.Trim();
        const string placeholder = "{loadFromConfig}";
        if (resolvedUrl.Contains(placeholder, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(loadFromConfigValue))
            {
                return false;
            }

            resolvedUrl = resolvedUrl.Replace(
                placeholder,
                Uri.EscapeDataString(loadFromConfigValue.Trim()),
                StringComparison.Ordinal
            );
        }

        if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var parsedUri))
        {
            return false;
        }

        serverUri = parsedUri;
        return true;
    }

    private static string RedactAlphaVantageApiKey(string serverUrl)
    {
        var trimmed = serverUrl.Trim();
        const string keyParameter = "apikey=";
        var keyIndex = trimmed.IndexOf(keyParameter, StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0)
        {
            return trimmed;
        }

        var valueStart = keyIndex + keyParameter.Length;
        var valueEnd = trimmed.IndexOf('&', valueStart);
        if (valueEnd < 0)
        {
            return $"{trimmed[..valueStart]}{{loadFromConfig}}";
        }

        return $"{trimmed[..valueStart]}{{loadFromConfig}}{trimmed[valueEnd..]}";
    }

    private sealed record ResolvedMcpServer(
        string Label,
        Uri Uri,
        string DisplayUri,
        string? Description,
        string? AuthorizationToken,
        McpToolCallApprovalPolicy ApprovalPolicy,
        string PurposeInstruction
    );

    private sealed record RuntimeAttempt(
        string Name,
        string Model,
        TradingAgentRuntimeRequest Request
    );

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
