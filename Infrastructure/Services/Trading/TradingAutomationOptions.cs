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
    /// Minimum breakout-candle range as a fraction of the opening range height.
    /// Filters out lifeless wick-style breakouts. 0 disables (default 0.30).
    /// </summary>
    public decimal BreakoutMinRangeFractionOfOpeningRange { get; set; } = 0.30m;

    /// <summary>
    /// Maximum allowed first-bar range as a fraction of the opening-range midpoint.
    /// Days that open with abnormally wide first-5-min ranges (gaps, news days) are
    /// skipped because the retest geometry doesn't apply cleanly. 0 disables.
    /// </summary>
    public decimal MaxOpeningRangeFractionOfPrice { get; set; } = 0.015m;

    /// <summary>
    /// When &gt; 0, position size is computed as floor(equity * fraction / riskPerUnit) and OrderQuantity is ignored.
    /// </summary>
    public decimal RiskPerTradeFraction { get; set; } = 0.0m;

    /// <summary>
    /// Minimum risk per unit as a fraction of entry price. Replaces the previous
    /// hardcoded $0.01 floor; expressed as a fraction so it scales to cheap stocks.
    /// Default 0.0005 = 5 bps of price.
    /// </summary>
    public decimal MinimumRiskPerUnitFraction { get; set; } = 0.0005m;

    /// <summary>
    /// When unrealized PnL reaches this many R, the stop is moved to entry price.
    /// 0 disables (default 1.0).
    /// </summary>
    public decimal BreakEvenAtRMultiple { get; set; } = 1.0m;

    /// <summary>
    /// Force-flat exit after this many post-entry 1-minute bars when neither the
    /// stop nor take-profit has hit. 0 disables (default 30 bars = 30 minutes).
    /// </summary>
    public int MaxBarsInTradeBeforeFlatExit { get; set; } = 30;

    /// <summary>
    /// Maximum trades simulated per backtest day. 0 disables (default 3).
    /// </summary>
    public int MaxTradesPerDay { get; set; } = 3;

    /// <summary>
    /// If cumulative PnL on a backtest day falls below -fraction * starting equity,
    /// no further trades are taken that day. 0 disables (default 0.02 = 2%).
    /// </summary>
    public decimal MaxDailyLossFraction { get; set; } = 0.02m;

    /// <summary>
    /// When true, the backtest fills a stop-out at the worse of the stop price and
    /// the post-entry bar's open (simulates gap-through-stop slippage). Default true.
    /// </summary>
    public bool BacktestStopSlippageOnGap { get; set; } = true;

    /// <summary>
    /// When true, BacktestEstimatedSpreadBps is treated as a baseline and scaled by
    /// price tier (cheap stocks get wider effective spreads). Default true.
    /// </summary>
    public bool BacktestSpreadBpsScaleByPrice { get; set; } = true;

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
        "You are a pre-market opportunity ranker for an intraday opening-range breakout strategy. Your job is to surface symbols likely to make a directional move during the first hour after the open. Inputs are pre-fetched in the payload: watchlist, Alpha Vantage NEWS_SENTIMENT (do not re-fetch), and prior-day daily candle. Use Alpaca MCP only to verify tradability and (when options are enabled) that both call and put contracts exist in the configured DTE window. Default to including a symbol unless evidence is clearly weak or contradictory; prefer false positives over false negatives. Either fresh aligned news OR a strong directional prior-day candle (close near high/low on expanded range and above-average volume) is sufficient on its own; both together is best. Treat missing or low-relevance news as neutral, not negative. Return between MIN and MAX opportunities and aim for the upper half of that range whenever the universe has plausible candidates. Score >=60 is tradeable; only return scores below that as exclusions. Return strict JSON only.";

    public string RetestValidationSystemPrompt { get; set; } =
        "You are a quality scorer for intraday breakout-and-retest candidates. The deterministic engine has ALREADY validated geometry, timing, acceptance, and pierce/near tolerances - your job is to confirm the candle data is real and to add price-action judgment, NOT to re-enforce those rules. Default to APPROVING the candidate (IsValidRetest=true). Veto only for clear, unambiguous problems you can see in the candles fetched via Alpaca MCP: data gaps or halts in the breakout-to-retest window, the levels in the payload not matching the actual first 5-minute candle, a close back inside the original range between breakout and retest that contradicts the payload, or the retest candle showing decisive failure of the level (close clearly on the wrong side by more than typical noise). Body colour by itself is NOT a veto - a red retest candle that wicks below the level and closes back above is a textbook bullish hold; a green retest candle that wicks above and closes back below is a textbook bearish hold. If a backtest causal cutoff is provided, evaluate only candles at or before it; do not penalize the candidate for confirmation that would only appear after the cutoff. Score 85-100 for clean candles with strong rejection wicks and good close location. Score 70-84 for valid setups with minor blemishes. Score 60-69 for marginal but still tradeable. Score below 60 only when you would actually veto. Set IsValidRetest=false only when you score below 60. Return strict JSON only.";

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
