using System.Text.Json;

namespace Application.Features.Trading.Automation;

public sealed class OpenAiTradingAgent : ITradingAgent
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = false,
    };

    private readonly ITradingAgentRuntime _runtime;

    public OpenAiTradingAgent(string name, string systemPrompt, ITradingAgentRuntime runtime)
    {
        Name = name;
        _runtime = runtime;
        SystemPrompt = systemPrompt;
    }

    public string Name { get; }

    public string SystemPrompt { get; }

    public async Task<TradingAgentExecutionResult> ExecuteAsync(
        TradingAgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        var userPrompt = BuildUserPrompt(context);
        var runtimeResponse = await _runtime.RunAsync(
            new TradingAgentRuntimeRequest(
                Name,
                SystemPrompt,
                userPrompt,
                context.Request.AgentMetadata
            ),
            cancellationToken
        );

        return new TradingAgentExecutionResult(
            Name,
            runtimeResponse.Text,
            runtimeResponse.StructuredOutput
        );
    }

    private static string BuildUserPrompt(TradingAgentContext context)
    {
        var payload = JsonSerializer.Serialize(context.Market, JsonSerializerOptions);
        return $"Analyze this market snapshot and provide guidance for your agent role: {payload}";
    }
}
