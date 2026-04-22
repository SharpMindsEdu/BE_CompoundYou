using System.Text.Json;
using Application.Features.Trading.Automation;
using Domain.Services.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public sealed class OpenAiTradingSignalAgent : ITradingSignalAgent
{
    private const string SentimentSchemaName = "watchlist_opportunities";
    private const string RetestSchemaName = "retest_validation";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly TradingAgentRuntimeJsonSchema SentimentJsonSchema = new(
        SentimentSchemaName,
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "Opportunities": {
              "type": "array",
              "maxItems": 3,
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "Symbol": { "type": "string" },
                  "Direction": { "type": "string", "enum": ["Bullish", "Bearish"] },
                  "Score": { "type": "integer", "minimum": 1, "maximum": 100 }
                },
                "required": ["Symbol", "Direction", "Score"]
              }
            }
          },
          "required": ["Opportunities"]
        }
        """
    );

    private static readonly TradingAgentRuntimeJsonSchema RetestJsonSchema = new(
        RetestSchemaName,
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "Symbol": { "type": "string" },
            "Direction": { "type": "string", "enum": ["Bullish", "Bearish"] },
            "Score": { "type": "integer", "minimum": 1, "maximum": 100 }
          },
          "required": ["Symbol", "Direction", "Score"]
        }
        """
    );

    private readonly ILogger<OpenAiTradingSignalAgent> _logger;
    private readonly IOptions<TradingAutomationOptions> _options;
    private readonly ITradingAgentRuntime _runtime;

    public OpenAiTradingSignalAgent(
        ITradingAgentRuntime runtime,
        IOptions<TradingAutomationOptions> options,
        ILogger<OpenAiTradingSignalAgent> logger
    )
    {
        _runtime = runtime;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TradingOpportunity>> AnalyzeWatchlistSentimentAsync(
        IReadOnlyCollection<string> symbols,
        int maxOpportunities,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedSymbols = symbols
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedSymbols.Length == 0)
        {
            return [];
        }

        var max = Math.Clamp(maxOpportunities, 1, 3);
        var payload = JsonSerializer.Serialize(
            new
            {
                Symbols = normalizedSymbols,
                MaxOpportunities = max,
            }
        );

        var runtimeResponse = await _runtime.RunAsync(
            new TradingAgentRuntimeRequest(
                AgentName: SentimentSchemaName,
                SystemPrompt: _options.Value.SentimentSystemPrompt,
                UserPrompt:
                $"Analyze watchlist sentiment and produce up to {max} opportunities for today's session. Payload: {payload}",
                JsonSchema: SentimentJsonSchema
            ),
            cancellationToken
        );

        if (!TryParse(runtimeResponse, out SentimentResponseDto? parsed) || parsed is null)
        {
            _logger.LogWarning("OpenAI sentiment response could not be parsed into opportunities JSON.");
            return [];
        }

        var opportunities = (parsed.Opportunities ?? Array.Empty<OpportunityDto?>())
            .Where(x => x is not null)
            .Select(x => x!)
            .Select(MapOpportunity)
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(x => normalizedSymbols.Contains(x.Symbol, StringComparer.OrdinalIgnoreCase))
            .Where(x => x.Score >= 1 && x.Score <= 100)
            .OrderByDescending(x => x.Score)
            .Take(max)
            .ToArray();

        return opportunities;
    }

    public async Task<RetestVerificationResult?> VerifyRetestAsync(
        RetestVerificationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                request.Symbol,
                Direction = request.Direction.ToString(),
                request.RangeUpper,
                request.RangeLower,
                BreakoutBar = new
                {
                    request.BreakoutBar.Timestamp,
                    request.BreakoutBar.Open,
                    request.BreakoutBar.High,
                    request.BreakoutBar.Low,
                    request.BreakoutBar.Close,
                    request.BreakoutBar.Volume,
                },
                RetestBar = new
                {
                    request.RetestBar.Timestamp,
                    request.RetestBar.Open,
                    request.RetestBar.High,
                    request.RetestBar.Low,
                    request.RetestBar.Close,
                    request.RetestBar.Volume,
                },
                RecentBars = request.RecentBars
                    .OrderBy(x => x.Timestamp)
                    .TakeLast(12)
                    .Select(x => new
                    {
                        x.Timestamp,
                        x.Open,
                        x.High,
                        x.Low,
                        x.Close,
                        x.Volume,
                    }),
            }
        );

        var runtimeResponse = await _runtime.RunAsync(
            new TradingAgentRuntimeRequest(
                AgentName: RetestSchemaName,
                SystemPrompt: _options.Value.RetestValidationSystemPrompt,
                UserPrompt:
                "Validate whether this breakout retest has strong continuation price action. Return only score if quality is high. Payload: "
                + payload,
                JsonSchema: RetestJsonSchema
            ),
            cancellationToken
        );

        if (!TryParse(runtimeResponse, out RetestResponseDto? parsed) || parsed is null)
        {
            _logger.LogWarning(
                "OpenAI retest response for {Symbol} could not be parsed into JSON.",
                request.Symbol
            );
            return null;
        }

        var direction = ToDirection(parsed.Direction);
        if (direction is null)
        {
            return null;
        }

        return new RetestVerificationResult(
            (parsed.Symbol ?? request.Symbol).Trim().ToUpperInvariant(),
            direction.Value,
            Math.Clamp(parsed.Score, 1, 100)
        );
    }

    private static TradingOpportunity? MapOpportunity(OpportunityDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Symbol))
        {
            return null;
        }

        var direction = ToDirection(dto.Direction);
        if (direction is null)
        {
            return null;
        }

        return new TradingOpportunity(
            dto.Symbol.Trim().ToUpperInvariant(),
            direction.Value,
            Math.Clamp(dto.Score, 1, 100)
        );
    }

    private static bool TryParse<T>(TradingAgentRuntimeResponse response, out T? value)
    {
        value = default;

        if (response.StructuredOutput.Count > 0)
        {
            var structuredJson = JsonSerializer.Serialize(response.StructuredOutput);
            if (
                TryDeserialize(structuredJson, out value)
                && value is not null
                && !structuredJson.Equals("{}", StringComparison.Ordinal)
            )
            {
                return true;
            }
        }

        if (TryDeserialize(response.Text, out value) && value is not null)
        {
            return true;
        }

        var trimmed = response.Text?.Trim() ?? string.Empty;
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            var jsonSlice = trimmed[firstBrace..(lastBrace + 1)];
            return TryDeserialize(jsonSlice, out value) && value is not null;
        }

        return false;
    }

    private static bool TryDeserialize<T>(string json, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
            return value is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static TradingDirection? ToDirection(string? direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
        {
            return null;
        }

        return direction.Trim().ToLowerInvariant() switch
        {
            "bullish" => TradingDirection.Bullish,
            "bearish" => TradingDirection.Bearish,
            _ => null,
        };
    }

    private sealed record SentimentResponseDto(IReadOnlyCollection<OpportunityDto?> Opportunities);

    private sealed record OpportunityDto(string Symbol, string Direction, int Score);

    private sealed record RetestResponseDto(string Symbol, string Direction, int Score);
}
