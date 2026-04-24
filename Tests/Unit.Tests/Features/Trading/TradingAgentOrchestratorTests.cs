using Application.Features.Trading.Automation;
using Domain.Services.Trading;

namespace Unit.Tests.Features.Trading;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TradingTests)]
public sealed class TradingAgentOrchestratorTests
{
    [Fact]
    public async Task RunAsync_BuildsMarketSnapshotAndSharesAgentOutputs()
    {
        var provider = new FakeTradingDataProvider();
        var firstAgent = new RecordingAgent(
            "first-agent",
            (context, _) =>
                Task.FromResult(
                    new TradingAgentExecutionResult(
                        "first-agent",
                        "first-summary",
                        new Dictionary<string, object?> { ["score"] = 80 }
                    )
                )
        );
        var secondAgent = new RecordingAgent(
            "second-agent",
            (context, _) =>
            {
                Assert.True(context.SharedMemory.ContainsKey("agent:first-agent"));
                return Task.FromResult(
                    new TradingAgentExecutionResult(
                        "second-agent",
                        "second-summary",
                        new Dictionary<string, object?> { ["action"] = "hold" }
                    )
                );
            }
        );
        var orchestrator = new TradingAgentOrchestrator([firstAgent, secondAgent], provider);

        var results = await orchestrator.RunAsync(
            new TradingAutomationRequest([" tsla ", "SPY", "TSLA", " "], BarsPerSymbol: 3),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, results.Count);
        Assert.Equal(["TSLA", "SPY"], provider.QuoteSymbols);
        Assert.Equal(
            ["TSLA:3", "SPY:3"],
            provider.BarRequests
        );
        Assert.Equal(1, firstAgent.ExecutionCount);
        Assert.Equal(1, secondAgent.ExecutionCount);
    }

    private sealed class RecordingAgent : ITradingAgent
    {
        private readonly Func<TradingAgentContext, CancellationToken, Task<TradingAgentExecutionResult>> _run;

        public RecordingAgent(
            string name,
            Func<TradingAgentContext, CancellationToken, Task<TradingAgentExecutionResult>> run
        )
        {
            Name = name;
            _run = run;
        }

        public string Name { get; }

        public int ExecutionCount { get; private set; }

        public async Task<TradingAgentExecutionResult> ExecuteAsync(
            TradingAgentContext context,
            CancellationToken cancellationToken = default
        )
        {
            ExecutionCount++;
            return await _run(context, cancellationToken);
        }
    }

    private sealed class FakeTradingDataProvider : ITradingDataProvider
    {
        public List<string> QuoteSymbols { get; } = [];
        public List<string> BarRequests { get; } = [];

        public Task<TradingAccountSnapshot> GetAccountAsync(
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(
                new TradingAccountSnapshot("acc-1", "ACTIVE", 1_000m, 1_000m, 1_000m, "USD")
            );
        }

        public Task<IReadOnlyCollection<TradingPositionSnapshot>> GetPositionsAsync(
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult<IReadOnlyCollection<TradingPositionSnapshot>>([]);
        }

        public Task<TradingQuoteSnapshot> GetQuoteAsync(
            string symbol,
            CancellationToken cancellationToken = default
        )
        {
            QuoteSymbols.Add(symbol);
            return Task.FromResult(
                new TradingQuoteSnapshot(symbol, 100m, 101m, 100.5m, DateTimeOffset.UtcNow)
            );
        }

        public Task<IReadOnlyCollection<TradingBarSnapshot>> GetRecentBarsAsync(
            string symbol,
            int limit = 50,
            CancellationToken cancellationToken = default
        )
        {
            BarRequests.Add($"{symbol}:{limit}");
            return Task.FromResult<IReadOnlyCollection<TradingBarSnapshot>>(
                [
                    new TradingBarSnapshot(
                        symbol,
                        DateTimeOffset.UtcNow,
                        100m,
                        101m,
                        99m,
                        100.5m,
                        1_000
                    ),
                ]
            );
        }

        public Task<IReadOnlyCollection<TradingBarSnapshot>> GetBarsAsync(
            string symbol,
            DateTimeOffset start,
            DateTimeOffset? end = null,
            int limit = 500,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<TradingBarSnapshot>> GetBarsInRangeAsync(
            string symbol,
            TradingBarInterval interval,
            DateTimeOffset start,
            DateTimeOffset end,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<string>> GetWatchlistSymbolsAsync(
            string watchlistId,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<DateTimeOffset?> GetWatchlistMarketOpenUtcAsync(
            string watchlistId,
            DateOnly tradingDate,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<TradingSessionSnapshot?> GetTradingSessionAsync(
            DateOnly tradingDate,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<TradingMarketClockSnapshot> GetMarketClockAsync(
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<TradingOrderSnapshot>> GetOpenOrdersAsync(
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<TradingOrderSubmissionResult> SubmitBracketOrderAsync(
            TradingBracketOrderRequest request,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<TradingOptionContractSnapshot?> SelectOptionContractAsync(
            string underlyingSymbol,
            TradingDirection direction,
            decimal underlyingPrice,
            DateOnly tradingDate,
            int minDaysToExpiration,
            int maxDaysToExpiration,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<TradingOptionQuoteSnapshot?> GetOptionQuoteAsync(
            string optionSymbol,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<TradingOrderSubmissionResult> SubmitOptionOrderAsync(
            TradingOptionOrderRequest request,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<TradingOrderSnapshot?> GetOrderAsync(
            string orderId,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }
    }
}
