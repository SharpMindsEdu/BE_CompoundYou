using Domain.Services.Trading;

namespace Application.Features.Trading.Automation;

public sealed record TradingAutomationRequest(
    IReadOnlyCollection<string> Symbols,
    int BarsPerSymbol = 50,
    IReadOnlyDictionary<string, string>? AgentMetadata = null
);

public sealed class TradingAgentContext
{
    public required TradingMarketSnapshot Market { get; init; }

    public required TradingAutomationRequest Request { get; init; }

    public IDictionary<string, object?> SharedMemory { get; } = new Dictionary<string, object?>();
}

public sealed record TradingAgentExecutionResult(
    string AgentName,
    string Summary,
    IReadOnlyDictionary<string, object?> Output
);

public interface ITradingAgent
{
    string Name { get; }

    Task<TradingAgentExecutionResult> ExecuteAsync(
        TradingAgentContext context,
        CancellationToken cancellationToken = default
    );
}

public interface ITradingAgentOrchestrator
{
    Task<IReadOnlyCollection<TradingAgentExecutionResult>> RunAsync(
        TradingAutomationRequest request,
        CancellationToken cancellationToken = default
    );
}
