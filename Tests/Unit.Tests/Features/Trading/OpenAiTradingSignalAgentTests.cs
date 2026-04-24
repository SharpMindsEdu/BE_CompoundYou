using Application.Features.Trading.Automation;
using Domain.Services.Trading;
using Infrastructure.Services.Trading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Unit.Tests.Features.Trading;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TradingTests)]
public sealed class OpenAiTradingSignalAgentTests
{
    [Fact]
    public async Task AnalyzeWatchlistSentimentAsync_ParsesAndFiltersRuntimeResponse()
    {
        var runtime = new FakeTradingAgentRuntime(
            new TradingAgentRuntimeResponse(
                """
                {
                  "Opportunities": [
                    { "Symbol": "TSLA", "Direction": "Bullish", "Score": 91 },
                    { "Symbol": "AAPL", "Direction": "Bearish", "Score": 88 },
                    { "Symbol": "MSFT", "Direction": "Bullish", "Score": 80 }
                  ]
                }
                """,
                new Dictionary<string, object?>()
            )
        );
        var agent = BuildAgent(runtime);

        var opportunities = await agent.AnalyzeWatchlistSentimentAsync(
            ["AAPL", "TSLA"],
            maxOpportunities: 2,
            tradingDate: new DateOnly(2026, 4, 22),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(2, opportunities.Count);
        Assert.Collection(
            opportunities,
            opportunity =>
            {
                Assert.Equal("TSLA", opportunity.Symbol);
                Assert.Equal(TradingDirection.Bullish, opportunity.Direction);
                Assert.Equal(91, opportunity.Score);
            },
            opportunity =>
            {
                Assert.Equal("AAPL", opportunity.Symbol);
                Assert.Equal(TradingDirection.Bearish, opportunity.Direction);
                Assert.Equal(88, opportunity.Score);
            }
        );
    }

    [Fact]
    public async Task VerifyRetestAsync_ParsesAndClampsScore()
    {
        var runtime = new FakeTradingAgentRuntime(
            new TradingAgentRuntimeResponse(
                """{ "Symbol": "TSLA", "Direction": "Bullish", "Score": 250 }""",
                new Dictionary<string, object?>()
            )
        );
        var agent = BuildAgent(runtime);
        var bars = new[]
        {
            new TradingBarSnapshot(
                "TSLA",
                new DateTimeOffset(2026, 4, 22, 13, 40, 0, TimeSpan.Zero),
                100m,
                101m,
                99m,
                100.5m,
                1_000
            ),
        };

        var result = await agent.VerifyRetestAsync(
            new RetestVerificationRequest(
                "TSLA",
                TradingDirection.Bullish,
                RangeUpper: 101m,
                RangeLower: 99m,
                BreakoutBar: bars[0],
                RetestBar: bars[0],
                RecentBars: bars
            ),
            tradingDate: new DateOnly(2026, 4, 22),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal("TSLA", result!.Symbol);
        Assert.Equal(TradingDirection.Bullish, result.Direction);
        Assert.Equal(100, result.Score);
    }

    [Fact]
    public async Task AnalyzeWatchlistSentimentAsync_EmbedsOptionsAndWatchlistRequirements_WhenOptionsTradingEnabled()
    {
        var runtime = new FakeTradingAgentRuntime(
            new TradingAgentRuntimeResponse(
                """
                {
                  "Opportunities": [
                    { "Symbol": "TSLA", "Direction": "Bullish", "Score": 90 }
                  ]
                }
                """,
                new Dictionary<string, object?>()
            )
        );
        var options = new TradingAutomationOptions
        {
            UseOptionsTrading = true,
            WatchlistId = "a5e81fdf-683a-4fc0-ae4a-e0ef2cea8e2e",
            OptionMinDaysToExpiration = 7,
            OptionMaxDaysToExpiration = 30,
        };
        var agent = BuildAgent(runtime, options);

        var opportunities = await agent.AnalyzeWatchlistSentimentAsync(
            ["TSLA"],
            maxOpportunities: 1,
            tradingDate: new DateOnly(2026, 4, 22),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Single(opportunities);
        Assert.NotNull(runtime.LastRequest);
        Assert.Contains(
            "Use Alpaca MCP tools to confirm option availability",
            runtime.LastRequest!.UserPrompt,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains(
            "Alpha Vantage MCP tools to fetch NEWS_SENTIMENT data",
            runtime.LastRequest.UserPrompt,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains(
            "a5e81fdf-683a-4fc0-ae4a-e0ef2cea8e2e",
            runtime.LastRequest.UserPrompt,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains(
            "7-30-day DTE window",
            runtime.LastRequest.UserPrompt,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task AnalyzeWatchlistSentimentAsync_PreservesSignalInsights()
    {
        var runtime = new FakeTradingAgentRuntime(
            new TradingAgentRuntimeResponse(
                """
                {
                  "Opportunities": [
                    {
                      "Symbol": "AAPL",
                      "Direction": "Bullish",
                      "Score": 92,
                      "OptionStrategyBias": "LongCall",
                      "SentimentScore": 0.42,
                      "SentimentLabel": "Somewhat-Bullish",
                      "SentimentRelevance": 0.97,
                      "SentimentSummary": "Fresh relevant news sentiment is positive.",
                      "CandleBias": "Bullish",
                      "CandleSummary": "Prior-day candle closed near the high on elevated volume.",
                      "Reason": "Sentiment and candle structure both support bullish continuation.",
                      "RiskNotes": "Setup weakens if price fades below the prior close."
                    }
                  ]
                }
                """,
                new Dictionary<string, object?>()
            )
        );
        var agent = BuildAgent(runtime);

        var opportunities = await agent.AnalyzeWatchlistSentimentAsync(
            ["AAPL"],
            maxOpportunities: 1,
            tradingDate: new DateOnly(2026, 4, 22),
            cancellationToken: TestContext.Current.CancellationToken
        );

        var opportunity = Assert.Single(opportunities);
        Assert.NotNull(opportunity.SignalInsights);
        Assert.Equal("LongCall", opportunity.SignalInsights!.OptionStrategyBias);
        Assert.Equal("Somewhat-Bullish", opportunity.SignalInsights.SentimentLabel);
        Assert.Equal("Prior-day candle closed near the high on elevated volume.", opportunity.SignalInsights.CandleSummary);
    }

    private static OpenAiTradingSignalAgent BuildAgent(
        ITradingAgentRuntime runtime,
        TradingAutomationOptions? options = null
    )
    {
        var wrappedOptions = Options.Create(options ?? new TradingAutomationOptions());
        return new OpenAiTradingSignalAgent(
            runtime,
            wrappedOptions,
            NullLogger<OpenAiTradingSignalAgent>.Instance
        );
    }

    private sealed class FakeTradingAgentRuntime : ITradingAgentRuntime
    {
        private readonly TradingAgentRuntimeResponse _response;

        public FakeTradingAgentRuntime(TradingAgentRuntimeResponse response)
        {
            _response = response;
        }

        public Task<TradingAgentRuntimeResponse> RunAsync(
            TradingAgentRuntimeRequest request,
            CancellationToken cancellationToken = default
        )
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }

        public TradingAgentRuntimeRequest? LastRequest { get; private set; }
    }
}
