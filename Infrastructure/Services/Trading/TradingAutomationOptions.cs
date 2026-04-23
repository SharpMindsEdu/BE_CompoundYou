namespace Infrastructure.Services.Trading;

public sealed class TradingAutomationOptions
{
    public const string SectionName = "TradingAutomation";

    public bool Enabled { get; set; } = false;

    public string TimeZoneId { get; set; } = "Eastern Standard Time";

    public int SentimentScanHour { get; set; } = 8;

    public int SentimentScanMinute { get; set; } = 30;

    public int MarketOpenHour { get; set; } = 9;

    public int MarketOpenMinute { get; set; } = 30;

    public int PollIntervalSeconds { get; set; } = 20;

    public string WatchlistId { get; set; } = string.Empty;

    public int MaxOpportunities { get; set; } = 3;

    public int MinimumSentimentScore { get; set; } = 70;

    public int MinimumRetestScore { get; set; } = 70;

    public decimal StopLossBufferPercent { get; set; } = 0.10m;

    public decimal RewardToRiskRatio { get; set; } = 2.0m;

    public bool UseOptionsTrading { get; set; } = true;

    public int OptionMinDaysToExpiration { get; set; } = 7;

    public int OptionMaxDaysToExpiration { get; set; } = 30;

    public bool UseWholeShareQuantity { get; set; } = true;

    public string StateFilePath { get; set; } = "artifacts/trading-automation-state.json";

    public int OrderQuantity { get; set; } = 10;

    public decimal BacktestStartingEquity { get; set; } = 100000m;

    public decimal BacktestEstimatedSpreadBps { get; set; } = 1.0m;

    public decimal BacktestEstimatedSlippageBps { get; set; } = 2.0m;

    public decimal BacktestCommissionPerUnit { get; set; } = 0m;

    public string SentimentSystemPrompt { get; set; } =
        "You are an institutional-grade market sentiment analyst. Rank only the strongest bullish or bearish opportunities from the provided watchlist using current sentiment, flow, and momentum context. Return strict JSON only.";

    public string RetestValidationSystemPrompt { get; set; } =
        "You are a strict intraday price action validator. Confirm whether a breakout retest shows strong continuation price action in the given direction. Return strict JSON only.";

    public List<TradingAutomationAgentOptions> Agents { get; set; } =
    [
        new()
        {
            Name = "risk-manager",
            SystemPrompt =
                "You are a risk management trading agent. Focus on capital preservation, position sizing, and stop-loss guidance.",
        },
        new()
        {
            Name = "signal-reviewer",
            SystemPrompt =
                "You are a market signal analysis trading agent. Focus on short-term trend, momentum, and reversal indicators.",
        },
    ];
}

public sealed class TradingAutomationAgentOptions
{
    public string Name { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;
}
