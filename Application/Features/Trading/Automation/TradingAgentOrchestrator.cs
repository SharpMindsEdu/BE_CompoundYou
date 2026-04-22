using Domain.Services.Trading;

namespace Application.Features.Trading.Automation;

public sealed class TradingAgentOrchestrator : ITradingAgentOrchestrator
{
    private readonly IReadOnlyCollection<ITradingAgent> _agents;
    private readonly ITradingDataProvider _tradingDataProvider;

    public TradingAgentOrchestrator(
        IEnumerable<ITradingAgent> agents,
        ITradingDataProvider tradingDataProvider
    )
    {
        _agents = agents.ToArray();
        _tradingDataProvider = tradingDataProvider;
    }

    public async Task<IReadOnlyCollection<TradingAgentExecutionResult>> RunAsync(
        TradingAutomationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var market = await BuildSnapshotAsync(request, cancellationToken);
        var context = new TradingAgentContext { Market = market, Request = request };

        var results = new List<TradingAgentExecutionResult>(_agents.Count);
        foreach (var agent in _agents)
        {
            var result = await agent.ExecuteAsync(context, cancellationToken);
            results.Add(result);
            context.SharedMemory[$"agent:{agent.Name}"] = result;
        }

        return results;
    }

    private async Task<TradingMarketSnapshot> BuildSnapshotAsync(
        TradingAutomationRequest request,
        CancellationToken cancellationToken
    )
    {
        var symbols = request.Symbols
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var accountTask = _tradingDataProvider.GetAccountAsync(cancellationToken);
        var positionsTask = _tradingDataProvider.GetPositionsAsync(cancellationToken);

        var quoteTasks = symbols.ToDictionary(
            symbol => symbol,
            symbol => _tradingDataProvider.GetQuoteAsync(symbol, cancellationToken),
            StringComparer.OrdinalIgnoreCase
        );

        var barTasks = symbols.ToDictionary(
            symbol => symbol,
            symbol => _tradingDataProvider.GetRecentBarsAsync(
                symbol,
                request.BarsPerSymbol,
                cancellationToken
            ),
            StringComparer.OrdinalIgnoreCase
        );

        await Task.WhenAll(
            [
                accountTask,
                positionsTask,
                ..quoteTasks.Values,
                ..barTasks.Values,
            ]
        );

        var quotes = quoteTasks.Values.Select(x => x.Result).ToArray();
        var barsBySymbol = barTasks.ToDictionary(
            x => x.Key,
            x => x.Value.Result,
            StringComparer.OrdinalIgnoreCase
        );

        return new TradingMarketSnapshot(
            accountTask.Result,
            positionsTask.Result,
            quotes,
            barsBySymbol
        );
    }
}
