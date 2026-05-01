using System.Globalization;
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
    private const int AlphaVantageNewsItemLimit = 8;
    private const string AlphaVantageNewsSentimentEndpoint = "https://www.alphavantage.co/query";
    
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
              "maxItems": 20,
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
    private readonly IOptions<OpenAiTradingOptions>? _openAiOptions;
    private readonly IHttpClientFactory? _httpClientFactory;
    public OpenAiTradingSignalAgent(
        ITradingAgentRuntime runtime,
        IOptions<TradingAutomationOptions> options,
        ILogger<OpenAiTradingSignalAgent> logger,
        IOptions<OpenAiTradingOptions>? openAiOptions = null,
        IHttpClientFactory? httpClientFactory = null
    )
    {
        _runtime = runtime;
        _options = options;
        _logger = logger;
        _openAiOptions = openAiOptions;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyCollection<TradingOpportunity>> AnalyzeWatchlistSentimentAsync(
        IReadOnlyCollection<string> symbols,
        int minOpportunities,
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
        var sentimentData = await LoadAlphaVantageSentimentDataAsync(
            normalizedSymbols,
            cancellationToken
        );
        var max = Math.Clamp(maxOpportunities, 1, 20);
        var min = Math.Clamp(minOpportunities, 1, max);
        var options = _options.Value;
        var payload = JsonSerializer.Serialize(
            new
            {
                Symbols = normalizedSymbols,
                MinOpportunities = min,
                MaxOpportunities = max,
                AlphaVantageNewsSentiment = sentimentData
            }
        );
        _logger.LogInformation("Start analyzing Sentiment for Trading.");

        var runtimeResponse = await _runtime.RunAsync(
            new TradingAgentRuntimeRequest(
                AgentName: SentimentSchemaName,
                SystemPrompt: options.SentimentSystemPrompt,
                UserPrompt: BuildSentimentUserPrompt(payload, min, max, tradingDate, options),
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
    
    private async Task<IReadOnlyCollection<AlphaVantageSymbolSentimentData>> LoadAlphaVantageSentimentDataAsync(
        IReadOnlyCollection<string> symbols,
        CancellationToken cancellationToken
    )
    {
        var apiKey = NormalizeText(_openAiOptions?.Value.AlphaVantageMcpApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return symbols
                .Select(symbol => new AlphaVantageSymbolSentimentData(
                    Symbol: symbol,
                    DataAvailable: false,
                    RetrievalNote: "Alpha Vantage API key is not configured.",
                    AggregateSentimentScore: null,
                    AggregateSentimentLabel: null,
                    AverageRelevance: null,
                    MatchingArticleCount: 0,
                    Articles: Array.Empty<AlphaVantageNewsArticleData>()
                ))
                .ToArray();
        }

        var httpClient = _httpClientFactory?.CreateClient();
        var disposeClient = false;
        if (httpClient is null)
        {
            httpClient = new HttpClient();
            disposeClient = true;
        }

        try
        {
            var items = new List<AlphaVantageSymbolSentimentData>(symbols.Count);
            foreach (var symbol in symbols)
            {
                items.Add(
                    await FetchAlphaVantageSentimentForSymbolAsync(
                        httpClient,
                        symbol,
                        apiKey,
                        cancellationToken
                    )
                );
            }

            return items;
        }
        finally
        {
            if (disposeClient)
            {
                httpClient.Dispose();
            }
        }
    }

    private async Task<AlphaVantageSymbolSentimentData> FetchAlphaVantageSentimentForSymbolAsync(
        HttpClient httpClient,
        string symbol,
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var requestUrl =
                $"{AlphaVantageNewsSentimentEndpoint}?function=NEWS_SENTIMENT&tickers={Uri.EscapeDataString(symbol)}&sort=LATEST&limit={AlphaVantageNewsItemLimit}&apikey={Uri.EscapeDataString(apiKey)}";

            using var response = await httpClient.GetAsync(requestUrl, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return CreateUnavailableSentimentData(
                    symbol,
                    $"Alpha Vantage HTTP {(int)response.StatusCode}."
                );
            }

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            var note = NormalizeText(TryGetStringProperty(root, "Information"))
                ?? NormalizeText(TryGetStringProperty(root, "Note"));
            if (note is not null)
            {
                return CreateUnavailableSentimentData(symbol, note);
            }

            var error = NormalizeText(TryGetStringProperty(root, "Error Message"));
            if (error is not null)
            {
                return CreateUnavailableSentimentData(symbol, error);
            }

            if (
                !root.TryGetProperty("feed", out var feed)
                || feed.ValueKind != JsonValueKind.Array
            )
            {
                return CreateUnavailableSentimentData(
                    symbol,
                    "Alpha Vantage response does not contain a valid feed array."
                );
            }

            var articles = ParseArticlesForSymbol(feed, symbol);
            if (articles.Count == 0)
            {
                return CreateUnavailableSentimentData(
                    symbol,
                    "No NEWS_SENTIMENT items matched this symbol."
                );
            }

            var aggregateScore = ComputeAggregateSentimentScore(articles);
            var averageRelevance = ComputeAverageRelevance(articles);

            return new AlphaVantageSymbolSentimentData(
                Symbol: symbol,
                DataAvailable: true,
                RetrievalNote: null,
                AggregateSentimentScore: aggregateScore,
                AggregateSentimentLabel: MapAggregateSentimentLabel(aggregateScore),
                AverageRelevance: averageRelevance,
                MatchingArticleCount: articles.Count,
                Articles: articles
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to fetch Alpha Vantage NEWS_SENTIMENT for symbol {Symbol}.",
                symbol
            );

            return CreateUnavailableSentimentData(
                symbol,
                "Alpha Vantage NEWS_SENTIMENT request failed."
            );
        }
    }

    private static AlphaVantageSymbolSentimentData CreateUnavailableSentimentData(
        string symbol,
        string reason
    )
    {
        return new AlphaVantageSymbolSentimentData(
            Symbol: symbol,
            DataAvailable: false,
            RetrievalNote: reason,
            AggregateSentimentScore: null,
            AggregateSentimentLabel: null,
            AverageRelevance: null,
            MatchingArticleCount: 0,
            Articles: Array.Empty<AlphaVantageNewsArticleData>()
        );
    }

    private static IReadOnlyCollection<AlphaVantageNewsArticleData> ParseArticlesForSymbol(
        JsonElement feed,
        string symbol
    )
    {
        var result = new List<AlphaVantageNewsArticleData>(AlphaVantageNewsItemLimit);

        foreach (var entry in feed.EnumerateArray())
        {
            if (!TryExtractTickerSentiment(entry, symbol, out var sentiment))
            {
                continue;
            }

            result.Add(
                new AlphaVantageNewsArticleData(
                    PublishedAt: NormalizeAlphaVantageTimestamp(
                        NormalizeText(TryGetStringProperty(entry, "time_published"))
                    ),
                    Source: NormalizeText(TryGetStringProperty(entry, "source")),
                    Title: NormalizeText(TryGetStringProperty(entry, "title")),
                    Url: NormalizeText(TryGetStringProperty(entry, "url")),
                    TickerSentimentScore: sentiment.Score,
                    TickerSentimentLabel: sentiment.Label,
                    RelevanceScore: sentiment.RelevanceScore,
                    OverallSentimentScore: TryParseDouble(
                        TryGetStringProperty(entry, "overall_sentiment_score")
                    ),
                    OverallSentimentLabel: NormalizeText(
                        TryGetStringProperty(entry, "overall_sentiment_label")
                    )
                )
            );

            if (result.Count >= AlphaVantageNewsItemLimit)
            {
                break;
            }
        }

        return result;
    }

    private static bool TryExtractTickerSentiment(
        JsonElement feedEntry,
        string symbol,
        out (double? Score, string? Label, double? RelevanceScore) sentiment
    )
    {
        sentiment = default;

        if (
            !feedEntry.TryGetProperty("ticker_sentiment", out var tickerSentiment)
            || tickerSentiment.ValueKind != JsonValueKind.Array
        )
        {
            return false;
        }

        foreach (var item in tickerSentiment.EnumerateArray())
        {
            var ticker = NormalizeText(TryGetStringProperty(item, "ticker"));
            if (!symbol.Equals(ticker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            sentiment = (
                Score: TryParseDouble(TryGetStringProperty(item, "ticker_sentiment_score")),
                Label: NormalizeText(TryGetStringProperty(item, "ticker_sentiment_label")),
                RelevanceScore: TryParseDouble(TryGetStringProperty(item, "relevance_score"))
            );

            return true;
        }

        return false;
    }

    private static string? TryGetStringProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static double? TryParseDouble(string? value)
    {
        if (
            string.IsNullOrWhiteSpace(value)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
        )
        {
            return null;
        }

        return parsed;
    }

    private static string? NormalizeAlphaVantageTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[] { "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm" };
        if (
            DateTime.TryParseExact(
                value.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed
            )
        )
        {
            return parsed.ToString("O", CultureInfo.InvariantCulture);
        }

        return value.Trim();
    }

    private static double? ComputeAggregateSentimentScore(
        IReadOnlyCollection<AlphaVantageNewsArticleData> articles
    )
    {
        var scored = articles
            .Where(x => x.TickerSentimentScore.HasValue)
            .Select(x => (Score: x.TickerSentimentScore!.Value, Relevance: x.RelevanceScore ?? 0d))
            .ToArray();
        if (scored.Length == 0)
        {
            return null;
        }

        var totalWeight = scored.Sum(x => x.Relevance);
        if (totalWeight <= 0)
        {
            return scored.Average(x => x.Score);
        }

        return scored.Sum(x => x.Score * x.Relevance) / totalWeight;
    }

    private static double? ComputeAverageRelevance(IReadOnlyCollection<AlphaVantageNewsArticleData> articles)
    {
        var relevanceValues = articles
            .Where(x => x.RelevanceScore.HasValue)
            .Select(x => x.RelevanceScore!.Value)
            .ToArray();
        if (relevanceValues.Length == 0)
        {
            return null;
        }

        return relevanceValues.Average();
    }

    private static string? MapAggregateSentimentLabel(double? aggregateScore)
    {
        if (!aggregateScore.HasValue)
        {
            return null;
        }

        return aggregateScore.Value switch
        {
            <= -0.35 => "Bearish",
            <= -0.15 => "Somewhat-Bearish",
            < 0.15 => "Neutral",
            < 0.35 => "Somewhat-Bullish",
            _ => "Bullish"
        };
    }

    private static string BuildRetestUserPrompt(
        string payload,
        DateOnly? tradingDate,
        DateTimeOffset? evaluationCutoffTimestampUtc
    )
    {
        var cutoffRule = evaluationCutoffTimestampUtc is null
            ? string.Empty
            : $"Causal cutoff: only use 1-minute candles with timestamp <= {evaluationCutoffTimestampUtc.Value:O}. Never use candles after this cutoff. If required confirmation only appears after the cutoff, mark the setup invalid due to insufficient causal confirmation.\n\n";

        return
            $"Validate an opening-range breakout-and-retest setup for {FormatTradingDate(tradingDate)}. " +
            "The payload only contains symbol, direction, expected range levels, and candidate timestamps. " +
            "Use Alpaca MCP tools to fetch the data yourself; do not invent candles.\n\n" +
            cutoffRule +

            "Data to fetch:\n" +
            "1. The first 5-minute regular-session candle - this defines the opening range high and low. Verify it matches the payload levels.\n" +
            "2. 1-minute candles from immediately after the opening range through (and including) the candidate retest timestamp.\n\n" +

            "Validation rules - all must hold in the requested direction:\n" +
            "1. Breakout candle: closes outside the opening range in the requested direction, with its close in the upper 60% (bullish) or lower 40% (bearish) of its own high-low.\n" +
            "2. Acceptance: at least two 1-minute closes outside the range, OR one breakout candle plus one further candle that closes outside AND in the directional 60/40 of its own range.\n" +
            "3. Continuity: no 1-minute candle between breakout and retest closes back inside the original range.\n" +
            "4. Retest geometry: the retest candle opens on the breakout side of the level, wicks back to within 10% of range-height of the level, does not pierce more than 20% of range-height past the level, and closes back on the breakout side.\n" +
            "5. Retest alignment: the retest candle close is on the breakout side of its open, OR the body is small (<= 10% of its own range) but still closes on the breakout side.\n" +
            "6. Timing: time between breakout and retest is <= 20 minutes.\n" +
            "7. Special rule: if candle 6 is an immediate breakout and candle 7 invalidates it with the opposite color and a close back inside the range, redraw the opening range to candles 1-6 before evaluating later breakouts.\n\n" +

            "Reject if any rule fails or if Alpaca data is insufficient to verify the full sequence.\n\n" +

            "Scoring:\n" +
            "- 90-100: All rules clean, multiple acceptance candles, controlled retest, strong directional retest candle.\n" +
            "- 75-89: Valid with minor imperfections.\n" +
            "- 60-74: Marginal but all rules met.\n" +
            "- Below 60: Invalid - set IsValidRetest=false.\n\n" +

            "Return JSON only using this structure (RetestCandleAligned is true when rule 5 passes):\n" +
            "{\n" +
            "  \"Symbol\": \"AAPL\",\n" +
            "  \"Direction\": \"Bullish\",\n" +
            "  \"IsValidRetest\": true,\n" +
            "  \"Score\": 82,\n" +
            "  \"OpeningRangeHigh\": 192.50,\n" +
            "  \"OpeningRangeLow\": 190.80,\n" +
            "  \"BreakoutConfirmed\": true,\n" +
            "  \"BreakoutQuality\": \"Strong\",\n" +
            "  \"BreakoutSummary\": \"Closed above the opening range high and held outside before retesting.\",\n" +
            "  \"RetestConfirmed\": true,\n" +
            "  \"RetestQuality\": \"Acceptable\",\n" +
            "  \"RetestSummary\": \"Pulled back near the opening range high and rejected the level as support.\",\n" +
            "  \"ConfirmationCandlePresent\": true,\n" +
            "  \"ContinuationBias\": \"Bullish\",\n" +
            "  \"InvalidationReason\": null,\n" +
            "  \"Reason\": \"Breakout, outside-range acceptance, and directional retest are aligned bullish.\",\n" +
            "  \"RiskNotes\": \"Setup fails if price closes back inside the opening range.\"\n" +
            "}\n\n" +

            "If invalid, still return all required fields, set IsValidRetest=false, Score<60, failed booleans to false, and explain in InvalidationReason.\n\n" +
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
    
    private sealed record AlphaVantageSymbolSentimentData(
        string Symbol,
        bool DataAvailable,
        string? RetrievalNote,
        double? AggregateSentimentScore,
        string? AggregateSentimentLabel,
        double? AverageRelevance,
        int MatchingArticleCount,
        IReadOnlyCollection<AlphaVantageNewsArticleData> Articles
    );

    private sealed record AlphaVantageNewsArticleData(
        string? PublishedAt,
        string? Source,
        string? Title,
        string? Url,
        double? TickerSentimentScore,
        string? TickerSentimentLabel,
        double? RelevanceScore,
        double? OverallSentimentScore,
        string? OverallSentimentLabel
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
    int minOpportunities,
    int maxOpportunities,
    DateOnly? tradingDate,
    TradingAutomationOptions options
)
{
    var basePrompt =
        $"Rank pre-market opportunities for the trading session on {FormatTradingDate(tradingDate)}. " +
        $"Return between {minOpportunities} and {maxOpportunities} opportunities when evidence allows; never below {minOpportunities} if symbols qualify, never above {maxOpportunities}. " +
        $"Payload: {payload}\n\n" +
        "Sentiment data (Alpha Vantage NEWS_SENTIMENT) is pre-fetched in the payload. Do NOT call Alpha Vantage MCP tools - use the payload values directly. If a symbol's DataAvailable is false, treat its sentiment as unavailable.\n\n" +

        "For each symbol, weigh:\n" +
        "1. Ticker-specific sentiment score and label, article relevance, recency (prefer last 24h), and source quality.\n" +
        "2. Prior trading day candle: direction, close location within the day's range, range expansion vs. prior days, volume vs. its 20-day average, and gap behavior.\n" +
        "3. Alignment between sentiment and candle structure - prefer agreement; penalize conflict.\n\n" +

        "Reject when sentiment is stale, low-relevance, or neutral; when sentiment and candle direction conflict; when prior-day volume is weak; or when candle direction is unclear.\n\n" +

        "Scoring:\n" +
        "- 90-100: Strong fresh aligned sentiment AND strong directional prior-day candle.\n" +
        "- 75-89: Mostly aligned with minor flaws.\n" +
        "- 60-74: Marginal but unconflicted.\n" +
        "- Below 60: Do not return.\n\n" +

        "Direction rules: Bullish = long call idea. Bearish = long put idea.\n\n" +

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
        "      \"RiskNotes\": \"Weakens if price fades below the prior close in pre-market.\"\n" +
        "    }\n" +
        "  ]\n" +
        "}\n\n" +
        "Use null for SentimentScore, SentimentLabel, or SentimentRelevance only when payload sentiment is unavailable or unverifiable. Return an empty Opportunities array only when nothing qualifies.";

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
