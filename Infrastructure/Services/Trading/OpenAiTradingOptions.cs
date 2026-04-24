namespace Infrastructure.Services.Trading;

public sealed class OpenAiTradingOptions
{
    public const string SectionName = "OpenAiTrading";

    public string BaseUrl { get; set; } = "https://api.openai.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4.1-mini";

    public string FallbackModel { get; set; } = string.Empty;

    public double Temperature { get; set; } = 0.2;

    public bool UseAutoTruncationMode { get; set; } = true;

    public bool EnableContextLengthRetry { get; set; } = true;

    public int MaxSystemPromptCharacters { get; set; } = 16_000;

    public int MaxUserPromptCharacters { get; set; } = 80_000;

    public int ContextRetrySystemPromptCharacters { get; set; } = 8_000;

    public int ContextRetryUserPromptCharacters { get; set; } = 24_000;

    public bool UseAlpacaMcpServer { get; set; } = true;

    public string AlpacaMcpServerLabel { get; set; } = "alpaca";

    public string AlpacaMcpServerDescription { get; set; } =
        "Alpaca trading and market data MCP server.";

    public string AlpacaMcpServerUrl { get; set; } = "https://alpaca.aboat-entertainment.com/mcp";

    public string AlpacaMcpAuthorization { get; set; } = string.Empty;

    public string AlpacaMcpRequireApproval { get; set; } = "never";

    public bool UseAlphaVantageMcpServer { get; set; } = true;

    public string AlphaVantageMcpServerLabel { get; set; } = "alphavantage";

    public string AlphaVantageMcpServerDescription { get; set; } =
        "Alpha Vantage market data and sentiment MCP server.";

    public string AlphaVantageMcpServerUrl { get; set; } =
        "https://mcp.alphavantage.co/mcp?apikey={loadFromConfig}";

    public string AlphaVantageMcpApiKey { get; set; } = string.Empty;
}
