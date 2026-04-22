namespace Infrastructure.Services.Trading;

public sealed class AlpacaTradingOptions
{
    public const string SectionName = "AlpacaTrading";

    public string BaseUrl { get; set; } = "https://paper-api.alpaca.markets";

    public string MarketDataUrl { get; set; } = "https://data.alpaca.markets";

    public string MarketDataFeed { get; set; } = "iex";

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;
}
