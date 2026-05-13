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
                SystemPrompt: AppendThresholdLine(
                    options.SentimentSystemPrompt,
                    options.MinimumSentimentScore
                ),
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
                SystemPrompt: AppendThresholdLine(
                    _options.Value.RetestValidationSystemPrompt,
                    _options.Value.MinimumRetestScore
                ),
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
            : $"Causal cutoff: evaluate only candles with timestamp <= {evaluationCutoffTimestampUtc.Value:O}. Do not penalize the candidate for confirmation that would only appear after this cutoff.\n\n";

        return
            $"Score a breakout-and-retest candidate for {FormatTradingDate(tradingDate)}. " +
            "The deterministic engine has already validated the geometric, timing, and acceptance rules - the candidate IS structurally valid. " +
            "Your job is to score quality and to veto only if the actual candle data contradicts the payload or shows a clear failure that the geometry could not see.\n\n" +
            cutoffRule +

            "Default position: APPROVE (IsValidRetest=true) with a score >=60. Lean toward approval.\n\n" +

            "Data to fetch via Alpaca MCP:\n" +
            "1. The first 5-minute regular-session candle - confirm its high/low match the OpeningRangeHigh/Low in the payload.\n" +
            "2. 1-minute candles from immediately after the opening range through the candidate retest timestamp.\n\n" +

            "Veto (set IsValidRetest=false, Score<60) ONLY for these unambiguous problems:\n" +
            "- The payload's opening-range levels do not match the actual first 5-minute candle.\n" +
            "- A 1-minute candle between breakout and retest closed back inside the original range (data contradicts the engine).\n" +
            "- Data gap, halt, or missing candles in the breakout-to-retest window.\n" +
            "- The retest candle's close is decisively on the WRONG side of the level (more than typical noise into the range).\n" +
            "- The whole sequence is unverifiable from Alpaca data.\n\n" +

            "DO NOT veto for these reasons - the engine has already handled them or they are not vetoes:\n" +
            "- Body colour. A red bullish-retest candle that wicks below the level and closes back above is a textbook hold; same logic mirrored for bearish. Score quality, do not reject.\n" +
            "- Volume. Low volume reduces score; it does not veto.\n" +
            "- Wick depth within the engine's pierce tolerance.\n" +
            "- Time between breakout and retest (the engine already capped this).\n\n" +

            "Quality scoring (use the full range):\n" +
            "- 85-100: Clean breakout candle with close in the directional 60/40 of its range, multiple confident acceptance closes outside, retest wick clearly rejects the level, retest closes well on the breakout side, healthy volume.\n" +
            "- 70-84: Valid with minor blemishes (one acceptance close instead of two strong ones, modestly weak volume, retest close only just on the breakout side).\n" +
            "- 60-69: Marginal but tradeable - geometry is OK, conviction is weak.\n" +
            "- Below 60: Only when you would actually veto.\n\n" +

            "Return JSON only using this structure:\n" +
            "{\n" +
            "  \"Symbol\": \"AAPL\",\n" +
            "  \"Direction\": \"Bullish\",\n" +
            "  \"IsValidRetest\": true,\n" +
            "  \"Score\": 78,\n" +
            "  \"OpeningRangeHigh\": 192.50,\n" +
            "  \"OpeningRangeLow\": 190.80,\n" +
            "  \"BreakoutConfirmed\": true,\n" +
            "  \"BreakoutQuality\": \"Acceptable\",\n" +
            "  \"BreakoutSummary\": \"Closed above the opening range high with a directional candle and held outside before retesting.\",\n" +
            "  \"RetestConfirmed\": true,\n" +
            "  \"RetestQuality\": \"Acceptable\",\n" +
            "  \"RetestSummary\": \"Wick dipped just below the opening range high and the candle closed back above; the level held.\",\n" +
            "  \"ConfirmationCandlePresent\": true,\n" +
            "  \"ContinuationBias\": \"Bullish\",\n" +
            "  \"InvalidationReason\": null,\n" +
            "  \"Reason\": \"Geometry validated by engine; price action confirms the level held.\",\n" +
            "  \"RiskNotes\": \"Stop fails if price closes back inside the opening range.\"\n" +
            "}\n\n" +

            "If you veto, return all required fields, set IsValidRetest=false, Score<60, BreakoutConfirmed/RetestConfirmed reflecting what you actually saw, and explain the specific issue in InvalidationReason.\n\n" +
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
        $"Return between {minOpportunities} and {maxOpportunities} opportunities and aim for the upper half of that range whenever the watchlist has plausible candidates. Never below {minOpportunities} if any symbols are at all plausible, never above {maxOpportunities}. " +
        $"Payload: {payload}\n\n" +
        "Sentiment data (Alpha Vantage NEWS_SENTIMENT) is pre-fetched in the payload. Do NOT call Alpha Vantage MCP tools - use the payload values directly. If a symbol's DataAvailable is false, treat its sentiment as NEUTRAL/MISSING (not negative). Missing news is not a reason to exclude.\n\n" +

        "Evaluation - either of these alone is sufficient to include a symbol; both together is best:\n" +
        "A. Fresh news (last 24h ideal, last 72h acceptable) with non-trivial relevance and a directional sentiment score.\n" +
        "B. A strong directional prior-day candle: close in the upper or lower 25% of the day's range, range expansion vs. recent days, or above-average volume.\n\n" +

        "Default to inclusion. Only EXCLUDE when sentiment and candle clearly contradict each other (fresh strongly-bearish news with a strong bullish prior-day candle, or vice versa). Ambiguity, missing news, average volume, or middling close-location are not reasons to exclude - they are reasons to score lower.\n\n" +

        "Scoring (use the full range, do NOT bunch up at the high end):\n" +
        "- 85-100: Strong fresh aligned news AND a strong directional prior-day candle.\n" +
        "- 70-84: One of A or B is clearly strong; the other is neutral or modestly supportive.\n" +
        "- 60-69: One of A or B is mildly supportive and nothing contradicts it.\n" +
        "- Below 60: Active contradiction between news and candle, or both signals are weak/missing.\n\n" +

        "Direction rules: Bullish = long call idea. Bearish = long put idea. If signals are mixed but one side dominates, pick that side and lower the score.\n\n" +

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

    /// <summary>
    /// Surfaces the bot's actual configured accept floor to the LLM so its score
    /// calibration matches what the bot enforces. Without this the LLM anchors
    /// to whatever number is in the static prompt body (typically 60) while the
    /// bot uses 65 / 75 — leaving signal in the gap unrecoverable.
    /// </summary>
    private static string AppendThresholdLine(string systemPrompt, int thresholdScore)
    {
        var clamped = Math.Clamp(thresholdScore, 1, 100);
        var thresholdLine =
            $"\n\nTHRESHOLD: The bot's actual minimum accept score is {clamped}. Calibrate your scoring distribution so this threshold meaningfully filters — do not score everything 80+ when {clamped} is the gate.";
        return string.IsNullOrWhiteSpace(systemPrompt)
            ? thresholdLine.TrimStart()
            : systemPrompt.TrimEnd() + thresholdLine;
    }
}
