namespace Infrastructure.Services.Trading;

public sealed class OpenAiTradingOptions
{
    public const string SectionName = "OpenAiTrading";

    public string BaseUrl { get; set; } = "https://api.openai.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4.1-mini";

    public double Temperature { get; set; } = 0.2;
}
