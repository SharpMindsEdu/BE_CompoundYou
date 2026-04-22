namespace Infrastructure.Services.Trading;

public sealed class OpenAiTradingOptions
{
    public const string SectionName = "OpenAiTrading";

    public string BaseUrl { get; set; } = "https://api.openai.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4.1-mini";

    public double Temperature { get; set; } = 0.2;

    public bool UseAlpacaMcpServer { get; set; } = true;

    public string AlpacaMcpServerLabel { get; set; } = "alpaca";

    public string AlpacaMcpServerDescription { get; set; } =
        "Alpaca trading and market data MCP server.";

    public string AlpacaMcpServerUrl { get; set; } = "https://alpaca.aboat-entertainment.com/mcp";

    public string AlpacaMcpAuthorization { get; set; } = string.Empty;

    public string AlpacaMcpRequireApproval { get; set; } = "never";
}
