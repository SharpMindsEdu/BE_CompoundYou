namespace Application.Features.Trading.Automation;

public sealed record TradingAgentRuntimeRequest(
    string AgentName,
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyDictionary<string, string>? Metadata = null
);

public sealed record TradingAgentRuntimeResponse(
    string Text,
    IReadOnlyDictionary<string, object?> StructuredOutput
);

public interface ITradingAgentRuntime
{
    Task<TradingAgentRuntimeResponse> RunAsync(
        TradingAgentRuntimeRequest request,
        CancellationToken cancellationToken = default
    );
}
