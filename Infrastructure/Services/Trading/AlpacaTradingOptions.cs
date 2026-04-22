namespace Infrastructure.Services.Trading;

public sealed class AlpacaTradingOptions
{
    public const string SectionName = "AlpacaTrading";

    public string BaseUrl { get; set; } = "https://paper-api.alpaca.markets";

    public string MarketDataUrl { get; set; } = "https://data.alpaca.markets";

    public string MarketDataFeed { get; set; } = "iex";

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public bool UseStreamingApi { get; set; } = true;

    public bool UseTradingStream { get; set; } = true;

    public bool UseMarketDataStream { get; set; } = true;

    public string TradingStreamUrl { get; set; } = "wss://paper-api.alpaca.markets/stream";

    public string MarketDataStreamUrl { get; set; } = "wss://stream.data.alpaca.markets/v2/iex";

    public int StreamingReconnectDelaySeconds { get; set; } = 5;

    public int StreamingMaxBarsPerSymbol { get; set; } = 2000;
}
