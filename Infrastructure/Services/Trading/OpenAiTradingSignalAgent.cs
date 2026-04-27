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
                  "Symbol": {
                    "type": "string"
                  },
                  "Direction": {
                    "type": "string",
                    "enum": ["Bullish", "Bearish"]
                  },
                  "Score": {
                    "type": "integer",
                    "minimum": 1,
                    "maximum": 100
                  },
                  "OptionStrategyBias": {
                    "type": "string",
                    "enum": ["LongCall", "LongPut", "ShortPut", "ShortCall", "Unknown"]
                  },
                  "SentimentScore": {
                    "type": ["number", "null"],
                    "minimum": -1,
                    "maximum": 1
                  },
                  "SentimentLabel": {
                    "type": ["string", "null"],
                    "enum": [
                      "Bearish",
                      "Somewhat-Bearish",
                      "Neutral",
                      "Somewhat-Bullish",
                      "Bullish",
                      null
                    ]
                  },
                  "SentimentRelevance": {
                    "type": ["number", "null"],
                    "minimum": 0,
                    "maximum": 1
                  },
                  "SentimentSummary": {
                    "type": "string"
                  },
                  "CandleBias": {
                    "type": "string",
                    "enum": ["Bullish", "Bearish", "Neutral", "Mixed"]
                  },
                  "CandleSummary": {
                    "type": "string"
                  },
                  "Reason": {
                    "type": "string"
                  },
                  "RiskNotes": {
                    "type": "string"
                  }
                },
                "required": [
                  "Symbol",
                  "Direction",
                  "Score",
                  "OptionStrategyBias",
                  "SentimentScore",
                  "SentimentLabel",
                  "SentimentRelevance",
                  "SentimentSummary",
                  "CandleBias",
                  "CandleSummary",
                  "Reason",
                  "RiskNotes"
                ]
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
        "Symbol": {
          "type": "string"
        },
        "Direction": {
          "type": "string",
          "enum": ["Bullish", "Bearish"]
        },
        "IsValidRetest": {
          "type": "boolean"
        },
        "Score": {
          "type": "integer",
          "minimum": 1,
          "maximum": 100
        },
        "OpeningRangeHigh": {
          "type": "number"
        },
        "OpeningRangeLow": {
          "type": "number"
        },
        "BreakoutConfirmed": {
          "type": "boolean"
        },
        "BreakoutQuality": {
          "type": "string",
          "enum": ["Strong", "Acceptable", "Weak", "Invalid"]
        },
        "BreakoutSummary": {
          "type": "string"
        },
        "RetestConfirmed": {
          "type": "boolean"
        },
        "RetestQuality": {
          "type": "string",
          "enum": ["Strong", "Acceptable", "Weak", "Invalid"]
        },
        "RetestSummary": {
          "type": "string"
        },
        "ConfirmationCandlePresent": {
          "type": "boolean"
        },
        "ContinuationBias": {
          "type": "string",
          "enum": ["Bullish", "Bearish", "Neutral", "Invalid"]
        },
        "InvalidationReason": {
          "type": ["string", "null"]
        },
        "Reason": {
          "type": "string"
        },
        "RiskNotes": {
          "type": "string"
        }
      },
      "required": [
        "Symbol",
        "Direction",
        "IsValidRetest",
        "Score",
        "OpeningRangeHigh",
        "OpeningRangeLow",
        "BreakoutConfirmed",
        "BreakoutQuality",
        "BreakoutSummary",
        "RetestConfirmed",
        "RetestQuality",
        "RetestSummary",
        "ConfirmationCandlePresent",
        "ContinuationBias",
        "InvalidationReason",
        "Reason",
        "RiskNotes"
      ]
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
        DateOnly? tradingDate = null,
        CancellationToken cancellationToken = default,
        Action<string>? onStreamingActivityDelta = null
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
        var options = _options.Value;
        var payload = JsonSerializer.Serialize(
            new
            {
                Symbols = normalizedSymbols,
                MaxOpportunities = max,
            }
        );
        _logger.LogInformation("Start analyzing Sentiment for Trading.");

        var runtimeResponse = await _runtime.RunAsync(
            new TradingAgentRuntimeRequest(
                AgentName: SentimentSchemaName,
                SystemPrompt: options.SentimentSystemPrompt,
                UserPrompt: BuildSentimentUserPrompt(payload, max, tradingDate, options),
                JsonSchema: SentimentJsonSchema,
                OnStreamingActivityDelta: onStreamingActivityDelta
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
        DateOnly? tradingDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                request.Symbol,
                Direction = request.Direction.ToString(),
                ExpectedOpeningRange = new
                {
                    High = request.RangeUpper,
                    Low = request.RangeLower
                },
                CandidateBreakoutTimestamp = request.BreakoutBar.Timestamp,
                CandidateRetestTimestamp = request.RetestBar.Timestamp,
                EvaluationCutoffTimestampUtc = request.EvaluationCutoffTimestampUtc,
                TradingDate = FormatTradingDate(tradingDate)
            }
        );
        
        var runtimeResponse = await _runtime.RunAsync(
            new TradingAgentRuntimeRequest(
                AgentName: RetestSchemaName,
                SystemPrompt: _options.Value.RetestValidationSystemPrompt,
                UserPrompt: BuildRetestUserPrompt(payload, tradingDate, request.EvaluationCutoffTimestampUtc),
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
            parsed.IsValidRetest,
            Math.Clamp(parsed.Score, 1, 100),
            parsed.OpeningRangeHigh,
            parsed.OpeningRangeLow,
            parsed.BreakoutConfirmed,
            NormalizeText(parsed.BreakoutQuality) ?? "Invalid",
            NormalizeText(parsed.BreakoutSummary) ?? string.Empty,
            parsed.RetestConfirmed,
            NormalizeText(parsed.RetestQuality) ?? "Invalid",
            NormalizeText(parsed.RetestSummary) ?? string.Empty,
            parsed.ConfirmationCandlePresent,
            NormalizeText(parsed.ContinuationBias) ?? "Invalid",
            NormalizeText(parsed.InvalidationReason),
            NormalizeText(parsed.Reason) ?? string.Empty,
            NormalizeText(parsed.RiskNotes) ?? string.Empty
        );
    }

    private static string BuildRetestUserPrompt(
        string payload,
        DateOnly? tradingDate,
        DateTimeOffset? evaluationCutoffTimestampUtc
    )
    {
        var cutoffRule = evaluationCutoffTimestampUtc is null
            ? string.Empty
            : $"Backtest causal rule: only use 1-minute candles with timestamp less than or equal to {evaluationCutoffTimestampUtc.Value:O}. Never use candles after this cutoff. If required confirmation would only appear after cutoff, mark the setup invalid due to insufficient causal confirmation.\n\n";

        return
            $"Validate whether Alpaca market data contains a valid opening-range breakout and retest setup for trading session date {FormatTradingDate(tradingDate)}. " +
            "Use the Alpaca MCP tools available in response options to fetch all required market data. " +
            "The payload only contains the requested symbol, direction, expected range levels, and candidate timestamps. " +
            "Do not expect candle data in the payload.\n\n" +
            cutoffRule +

            "Required data fetching:\n" +
            "1. Fetch the first 5-minute regular-session candle for the symbol on the trading date.\n" +
            "2. Use that candle to verify the opening range high and opening range low.\n" +
            "3. Fetch 1-minute candles from immediately after the first 5-minute candle through the candidate retest timestamp, and do not fetch/use any candles after EvaluationCutoffTimestampUtc when it is present.\n" +
            "4. Use the 1-minute candles to validate breakout, acceptance outside the range, retest, and confirmation.\n\n" +

            "Strategy definition:\n" +
            "- Opening range high is the high of the first 5-minute candle of the regular trading session.\n" +
            "- Opening range low is the low of the first 5-minute candle of the regular trading session.\n" +
            "- Bullish setup: price breaks above opening range high, holds outside the range, retests the opening range high as support, then confirms upward continuation.\n" +
            "- Bearish setup: price breaks below opening range low, holds outside the range, retests the opening range low as resistance, then confirms downward continuation.\n\n" +

            "Validation requirements:\n" +
            "1. Confirm the requested Direction matches the breakout side.\n" +
            "2. Confirm price closed outside the opening range in the requested direction.\n" +
            "3. Confirm the breakout is not just a single candle wick outside the range.\n" +
            "4. Confirm there is acceptance outside the range before the retest. Prefer at least two 1-minute closes outside the range or a clear continuation candle before pullback.\n" +
            "5. Confirm the retest returns near the broken level without fully invalidating the breakout.\n" +
            "6. Confirm the retest candle or following candle shows rejection in the breakout direction.\n" +
            "7. Reject if price closes back inside the original 5-minute range after breakout before confirmation.\n" +
            "8. Reject if the retest happens immediately after only one breakout candle without outside-range acceptance.\n" +
            "9. Reject if the retest candle breaks too far through the level and does not reclaim or reject it.\n" +
            "10. Reject if candle direction, wick structure, close location, or volume does not support continuation.\n" +
            "11. Reject if Alpaca data cannot be fetched or is insufficient to verify the full sequence.\n\n" +

            "Bullish confirmation examples:\n" +
            "- Retest wick touches or slightly dips near the opening range high and closes back above it.\n" +
            "- Confirmation candle closes above the retest candle high.\n" +
            "- Bullish candle body closes in the upper part of its range.\n" +
            "- Pullback volume is controlled and continuation volume improves.\n\n" +

            "Bearish confirmation examples:\n" +
            "- Retest wick touches or slightly pushes near the opening range low and closes back below it.\n" +
            "- Confirmation candle closes below the retest candle low.\n" +
            "- Bearish candle body closes in the lower part of its range.\n" +
            "- Pullback volume is controlled and continuation volume improves.\n\n" +

            "Scoring guidance:\n" +
            "- 90-100: Clean breakout, multiple candles of acceptance outside the range, controlled retest, strong confirmation candle.\n" +
            "- 75-89: Valid breakout and retest with good confirmation, but minor imperfections.\n" +
            "- 60-74: Acceptable setup, but only if breakout, retest, and confirmation are all present.\n" +
            "- Below 60: Invalid or too weak. Set IsValidRetest to false.\n\n" +

            "Return JSON only using this structure:\n" +
            "{\n" +
            "  \"Symbol\": \"AAPL\",\n" +
            "  \"Direction\": \"Bullish\",\n" +
            "  \"IsValidRetest\": true,\n" +
            "  \"Score\": 82,\n" +
            "  \"OpeningRangeHigh\": 192.50,\n" +
            "  \"OpeningRangeLow\": 190.80,\n" +
            "  \"BreakoutConfirmed\": true,\n" +
            "  \"BreakoutQuality\": \"Strong\",\n" +
            "  \"BreakoutSummary\": \"Alpaca 1-minute candles show price closed above the opening range high and held outside the range before retesting.\",\n" +
            "  \"RetestConfirmed\": true,\n" +
            "  \"RetestQuality\": \"Acceptable\",\n" +
            "  \"RetestSummary\": \"Price pulled back near the opening range high and rejected the level as support.\",\n" +
            "  \"ConfirmationCandlePresent\": true,\n" +
            "  \"ContinuationBias\": \"Bullish\",\n" +
            "  \"InvalidationReason\": null,\n" +
            "  \"Reason\": \"Breakout, outside-range acceptance, retest, and confirmation are aligned in the bullish direction.\",\n" +
            "  \"RiskNotes\": \"Setup fails if price closes back inside the opening range.\"\n" +
            "}\n\n" +

            "If invalid, still return all required fields, set IsValidRetest to false, set Score below 60, set failed booleans to false, and explain the invalidation reason.\n\n" +
            "Payload: " + payload;
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
            Math.Clamp(dto.Score, 1, 100),
            BuildSignalInsights(dto)
        );
    }

    private static TradingSignalInsights? BuildSignalInsights(OpportunityDto dto)
    {
        var insights = new TradingSignalInsights(
            OptionStrategyBias: NormalizeText(dto.OptionStrategyBias),
            SentimentScore: dto.SentimentScore,
            SentimentLabel: NormalizeText(dto.SentimentLabel),
            SentimentRelevance: dto.SentimentRelevance,
            SentimentSummary: NormalizeText(dto.SentimentSummary),
            CandleBias: NormalizeText(dto.CandleBias),
            CandleSummary: NormalizeText(dto.CandleSummary),
            Reason: NormalizeText(dto.Reason),
            RiskNotes: NormalizeText(dto.RiskNotes)
        );

        return HasSignalInsights(insights) ? insights : null;
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

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool HasSignalInsights(TradingSignalInsights insights)
    {
        return !string.IsNullOrWhiteSpace(insights.OptionStrategyBias)
            || insights.SentimentScore.HasValue
            || !string.IsNullOrWhiteSpace(insights.SentimentLabel)
            || insights.SentimentRelevance.HasValue
            || !string.IsNullOrWhiteSpace(insights.SentimentSummary)
            || !string.IsNullOrWhiteSpace(insights.CandleBias)
            || !string.IsNullOrWhiteSpace(insights.CandleSummary)
            || !string.IsNullOrWhiteSpace(insights.Reason)
            || !string.IsNullOrWhiteSpace(insights.RiskNotes);
    }

    private sealed record SentimentResponseDto(
        IReadOnlyCollection<OpportunityDto?> Opportunities
    );

    private sealed record OpportunityDto(
        string Symbol,
        string? Direction,
        int Score,
        string? OptionStrategyBias,
        double? SentimentScore,
        string? SentimentLabel,
        double? SentimentRelevance,
        string? SentimentSummary,
        string? CandleBias,
        string? CandleSummary,
        string? Reason,
        string? RiskNotes
    );

    private enum SentimentDirection
    {
        Bullish,
        Bearish
    }

    private enum OptionStrategyBias
    {
        LongCall,
        LongPut,
        ShortPut,
        ShortCall,
        Unknown
    }

    private enum SentimentLabel
    {
        Bearish,
        SomewhatBearish,
        Neutral,
        SomewhatBullish,
        Bullish
    }

    private enum CandleBias
    {
        Bullish,
        Bearish,
        Neutral,
        Mixed
    }

    private sealed record RetestResponseDto(
        string Symbol,
        string Direction,
        bool IsValidRetest,
        int Score,
        decimal OpeningRangeHigh,
        decimal OpeningRangeLow,
        bool BreakoutConfirmed,
        string BreakoutQuality,
        string BreakoutSummary,
        bool RetestConfirmed,
        string RetestQuality,
        string RetestSummary,
        bool ConfirmationCandlePresent,
        string ContinuationBias,
        string? InvalidationReason,
        string Reason,
        string RiskNotes
    );
private static string BuildSentimentUserPrompt(
    string payload,
    int maxOpportunities,
    DateOnly? tradingDate,
    TradingAutomationOptions options
)
{
    var basePrompt =
        $"Analyze the provided watchlist symbols for the pre-market trading session on {FormatTradingDate(tradingDate)}. " +
        $"Produce up to {maxOpportunities} trade opportunities. " +
        $"Payload: {payload}\n\n" +
        "Use the Alpha Vantage MCP tools available in response options to fetch NEWS_SENTIMENT data, and use Alpaca MCP tools to verify tradability and option availability.\n\n" +

        "Analysis requirements:\n" +
        "1. For each symbol, analyze Alpha Vantage NEWS_SENTIMENT data when available.\n" +
        "2. Consider ticker-specific sentiment score, sentiment label, relevance score, article recency, article source quality, and whether the news is directly related to the symbol.\n" +
        "3. Analyze the prior trading day candles, including open, high, low, close, volume, gap behavior, range expansion, trend direction, close location within the daily range, and unusual volume.\n" +
        "4. Combine sentiment and candle evidence into one directional trade thesis.\n" +
        "5. Prefer symbols where sentiment and candle structure agree.\n" +
        "6. Penalize symbols with stale news, low relevance scores, neutral sentiment, contradictory headlines, weak volume, or unclear candle direction.\n" +
        "7. Keep MCP payloads compact: for Alpha Vantage NEWS_SENTIMENT use ticker-specific queries, sort latest, and limit to at most 8 items per symbol.\n" +
        "8. Do not force trades. Return fewer than the requested maximum if the evidence is not strong enough.\n\n" +

        "Scoring guidance:\n" +
        "- 90-100: Very strong alignment between fresh relevant sentiment and strong prior-day candle confirmation.\n" +
        "- 75-89: Strong setup with mostly aligned sentiment and candle evidence.\n" +
        "- 60-74: Moderate setup, acceptable only if evidence is clear and not conflicting.\n" +
        "- Below 60: Do not return as an opportunity.\n\n" +

        "Direction rules:\n" +
        "- Bullish means the symbol favors a long call idea.\n" +
        "- Bearish means the symbol favors a long put idea.\n" +
        "- If sentiment and candles conflict, either lower the score significantly or exclude the symbol.\n\n" +

        "Return format:\n" +
        "Return a JSON array only. Each item must contain:\n" +
        "Return JSON only using this structure:\n" +
        "{\n" +
        "  \"Opportunities\": [\n" +
        "    {\n" +
        "      \"Symbol\": \"AAPL\",\n" +
        "      \"Direction\": \"Bullish\",\n" +
        "      \"Score\": 82,\n" +
        "      \"OptionStrategyBias\": \"LongCall\",\n" +
        "      \"SentimentScore\": 0.28,\n" +
        "      \"SentimentLabel\": \"Somewhat-Bullish\",\n" +
        "      \"SentimentRelevance\": 1.0,\n" +
        "      \"SentimentSummary\": \"Fresh relevant news sentiment is positive.\",\n" +
        "      \"CandleBias\": \"Bullish\",\n" +
        "      \"CandleSummary\": \"Prior-day candle closed near the high on elevated volume.\",\n" +
        "      \"Reason\": \"Sentiment and candle structure both support bullish continuation.\",\n" +
        "      \"RiskNotes\": \"Setup weakens if price fades below the prior close in pre-market.\"\n" +
        "    }\n" +
        "  ]\n" +
        "}\n\n" +
        "Use null for SentimentScore, SentimentLabel, or SentimentRelevance only when Alpha Vantage NEWS_SENTIMENT data is unavailable or unverifiable.\n" +
        "Return an empty Opportunities array when no setup qualifies";

    if (!options.UseOptionsTrading)
    {
        return basePrompt;
    }

    var watchlistId = options.WatchlistId?.Trim();
    var watchlistText = string.IsNullOrWhiteSpace(watchlistId)
        ? "the configured Alpaca watchlist"
        : $"Alpaca watchlist '{watchlistId}'";

    var minDte = Math.Max(0, options.OptionMinDaysToExpiration);
    var maxDte = Math.Max(minDte, options.OptionMaxDaysToExpiration);

    return
        basePrompt + "\n\n" +
        "Options-only execution is enabled.\n" +
        $"Use Alpaca MCP tools to confirm option availability for symbols from {watchlistText}.\n" +
        $"Only return symbols with tradable call and put contracts in the configured {minDte}-{maxDte}-day DTE window.\n" +
        "Confirm that the selected symbol supports the recommended option side.\n" +
        "Align bullish opportunities to calls and bearish opportunities to puts.\n" +
        "Exclude the symbol if option availability cannot be verified.";
}

    private static string FormatTradingDate(DateOnly? tradingDate)
    {
        return tradingDate?.ToString("yyyy-MM-dd") ?? "today";
    }
}
