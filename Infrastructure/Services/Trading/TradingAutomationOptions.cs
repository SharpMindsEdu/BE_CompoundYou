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

    public int MaxOpportunities { get; set; } = 10;
    
    public int MinOpportunities { get; set; } = 3;

    public int MinimumSentimentScore { get; set; } = 70;

    public int MinimumRetestScore { get; set; } = 70;

    public bool UseRetestValidationAgent { get; set; } = true;

    public int MinimumMinutesFromMarketOpenForEntry { get; set; } = 10;

    public int MaximumMinutesFromMarketOpenForEntry { get; set; } = 60;

    public decimal MinimumEntryDistanceFromRangeFraction { get; set; } = 0.0m;

    public int MaxMinutesBreakoutToRetest { get; set; } = 20;

    public bool BacktestAllowOppositeDirectionFallback { get; set; } = false;

    /// <summary>
    /// Stop-loss buffer expressed as a fraction of price (e.g. 0.005 = 0.5%).
    /// Applied to the retest bar extreme to widen the stop and absorb noise.
    /// </summary>
    public decimal StopLossBufferFraction { get; set; } = 0.005m;

    public decimal RewardToRiskRatio { get; set; } = 2.0m;

    public decimal BreakoutDirectionalCloseLocationThreshold { get; set; } = 0.60m;

    public decimal RetestNearRangeFraction { get; set; } = 0.10m;

    public decimal RetestPierceRangeFraction { get; set; } = 0.20m;

    public decimal RetestBodyToleranceFraction { get; set; } = 0.10m;

    /// <summary>
    /// When &gt; 0, position size is computed as floor(equity * fraction / riskPerUnit) and OrderQuantity is ignored.
    /// </summary>
    public decimal RiskPerTradeFraction { get; set; } = 0.0m;

    /// <summary>
    /// Minutes before session close at which open positions are force-exited
    /// in the backtest. 0 disables the cutoff (default: 5).
    /// </summary>
    public int EndOfDayExitBufferMinutes { get; set; } = 5;

    public bool UseOptionsTrading { get; set; } = true;

    public int OptionMinDaysToExpiration { get; set; } = 7;

    public int OptionMaxDaysToExpiration { get; set; } = 30;

    public bool UseWholeShareQuantity { get; set; } = true;

    public string StateFilePath { get; set; } = "artifacts/trading-automation-state.json";

    public int OrderQuantity { get; set; } = 10;

    public decimal BacktestStartingEquity { get; set; } = 100000m;

    public decimal BacktestEstimatedSpreadBps { get; set; } = 1.0m;

    public decimal BacktestMarketOrderSpreadFillRatio { get; set; } = 0.85m;

    public decimal BacktestEstimatedSlippageBps { get; set; } = 2.0m;

    public decimal BacktestCommissionPerUnit { get; set; } = 0m;

    public bool BacktestUseAlpacaStandardFees { get; set; } = true;

    public decimal BacktestAlpacaSecFeePerMillionSold { get; set; } = 20.60m;

    public decimal BacktestAlpacaTafFeePerShareSold { get; set; } = 0.000195m;

    public decimal BacktestAlpacaTafMaxPerTrade { get; set; } = 9.79m;

    public decimal BacktestAlpacaSellSideMinimumFee { get; set; } = 0.01m;

    public bool BacktestUseTrailingStopLoss { get; set; } = false;

    public bool BacktestUseAiSentiment { get; set; } = false;

    public bool BacktestUseAiRetestValidationAgent { get; set; } = false;

    public decimal BacktestPartialTakeProfitFraction { get; set; } = 0.5m;

    public decimal BacktestTrailingStopRiskMultiple { get; set; } = 1.0m;

    public bool BacktestTrailingStopBreakEvenProtection { get; set; } = true;

    public bool BacktestCandleCacheEnabled { get; set; } = true;

    public int BacktestCandleCacheTtlMinutes { get; set; } = 180;

    public int BacktestCandleCacheMaxEntries { get; set; } = 5000;

    public bool LiveUseTrailingStopLoss { get; set; } = false;

    public decimal LivePartialTakeProfitFraction { get; set; } = 0.5m;

    public decimal LiveTrailingStopRiskMultiple { get; set; } = 1.0m;

    public bool LiveTrailingStopBreakEvenProtection { get; set; } = true;

    public string SentimentSystemPrompt { get; set; } =
        "You are a pre-market opportunity ranker for an opening-range breakout strategy. Inputs: a watchlist with pre-fetched Alpha Vantage NEWS_SENTIMENT data and the prior trading day's daily candle. Do not call MCP for sentiment - it is already in the payload. Use Alpaca MCP only to verify that each candidate is tradable and (when options trading is enabled) has both call and put contracts available in the configured DTE window. For each symbol weigh: ticker-specific sentiment score and label, article relevance, recency (prefer last 24h), source quality, and the prior daily candle's direction, close-location within its range, range expansion, and volume vs. its 20-day average. Reject when sentiment and candle disagree, when news is stale or low-relevance, or when option contracts cannot be verified. Return between MIN and MAX opportunities; never below MIN if evidence allows, never above MAX. Return strict JSON only.";

    public string RetestValidationSystemPrompt { get; set; } =
        "You are a strict intraday opening-range breakout-and-retest validator. Use Alpaca MCP tools to fetch the data yourself; do not invent candles or volumes. Respect any backtest causal cutoff exactly. The opening range is the high/low of the first 5-minute regular-session candle. After that, evaluate 1-minute candles. A setup is valid only if all of these hold in the requested direction: (1) a breakout candle closes outside the range with its close in the upper 60% (bullish) or lower 40% (bearish) of its own high-low; (2) acceptance: at least two 1-minute closes outside the range, or one breakout candle plus one further candle that closes outside and in the directional 60/40 of its own range; (3) no 1-minute candle between breakout and retest closes back inside the original range; (4) the retest candle opens on the breakout side of the level, wicks back to within 10% of range-height of the level, does not pierce more than 20% of range-height past the level, and closes back on the breakout side; (5) the retest candle is directionally aligned: close is on the breakout side of its open OR the body is small (under 10% of its range) but still closes on the breakout side; (6) time between breakout and retest is at most 20 minutes. Score 90-100 for clean alignment, 75-89 for valid with minor flaws, 60-74 for marginal, below 60 for invalid. Set IsValidRetest=false if any rule fails or data is insufficient. Return strict JSON only.";

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
