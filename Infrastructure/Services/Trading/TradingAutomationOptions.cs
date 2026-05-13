using Application.Features.Trading.Automation;

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

    public int MinCandlesBetweenBreakoutAndRetest { get; set; } = 5;

    public bool BacktestAllowOppositeDirectionFallback { get; set; } = false;

    /// <summary>
    /// Price-relative stop buffer floor as a fraction of the retest bar extreme
    /// (e.g. 0.0005 = 5 bps). Used only as a safety floor when the retest candle's
    /// range is degenerate; the primary buffer is range-relative below.
    /// </summary>
    public decimal StopLossBufferFraction { get; set; } = 0.0005m;

    /// <summary>
    /// Stop buffer as a fraction of the retest bar's own high-low range. This is
    /// the primary buffer mechanism: a 10% setting on a $0.40-range retest candle
    /// places the stop $0.04 below the low (tight, volatility-aware).
    /// </summary>
    public decimal StopLossBufferAsRetestRangeFraction { get; set; } = 0.10m;

    public decimal RewardToRiskRatio { get; set; } = 2.0m;

    /// <summary>
    /// Bullish breakout candles must close in the top <c>1 - threshold</c> of their range
    /// (and the symmetric inverse for bearish). 0.70 means close in the top 30%
    /// — a stronger signal than the previous default of 0.60.
    /// </summary>
    public decimal BreakoutDirectionalCloseLocationThreshold { get; set; } = 0.70m;

    public decimal RetestNearRangeFraction { get; set; } = 0.10m;

    public decimal RetestPierceRangeFraction { get; set; } = 0.20m;

    public decimal RetestBodyToleranceFraction { get; set; } = 0.10m;

    /// <summary>
    /// Tolerance (as a fraction of opening-range height) allowing the retest
    /// candle to OPEN slightly on the wrong side of the level. Catches textbook
    /// "spring"/bear-trap rejection patterns where price opens just below the
    /// broken-out level, wicks down, then closes back above. 0 disables the
    /// loosening (strict-open behaviour); default 0.30.
    /// </summary>
    public decimal RetestOpenToleranceFraction { get; set; } = 0.30m;

    /// <summary>
    /// Tolerance (as a fraction of opening-range height) allowing the retest
    /// candle's close to sit fractionally below the breakout level. Tiny by
    /// design — the close is the strongest "did the level hold?" signal and
    /// shouldn't be relaxed much. Default 0.05.
    /// </summary>
    public decimal RetestCloseToleranceFraction { get; set; } = 0.05m;

    /// <summary>
    /// Volume sanity check on the retest candle: when &gt; 0, the retest's
    /// volume must be at or below <c>multiplier × breakoutBar.Volume</c>.
    /// Real retests typically print on lower volume than the breakout
    /// (exhausted sellers); a louder retest candle is usually fresh selling
    /// pressure. 0 disables. Default 0.8 (quality-over-quantity baseline).
    /// </summary>
    public decimal RetestMaxVolumeFractionOfBreakout { get; set; } = 0.8m;

    /// <summary>
    /// When true, the stop-loss is anchored at the worse of the retest extreme
    /// and the breakout-bar extreme (min low for longs, max high for shorts).
    /// Respects pre-breakout swing structure and reduces choppy stop-outs on
    /// unusually tight retest candles, at the cost of a wider initial stop.
    /// Default true (quality-over-quantity baseline).
    /// </summary>
    public bool StopAnchorToSwingExtreme { get; set; } = true;

    /// <summary>
    /// When true, the worker only enters a trade whose direction agrees with
    /// the prior trading day's daily-candle direction (bullish if prior close
    /// &gt; prior open; bearish if &lt;). A coarse but cheap "do not fight the
    /// daily" filter that throws out the worst opening-range setups (e.g. long
    /// the bounce on a stock that just printed a strong red daily). Default
    /// false.
    /// </summary>
    public bool RequirePriorDayDirectionalAlignment { get; set; } = false;

    /// <summary>
    /// When true and the deterministic confidence signal is high, the worker
    /// skips the OpenAI retest validation call (latency win on the entry path).
    /// "High confidence" requires at least <see cref="DeterministicMarginalSignalsRequired"/>
    /// of the configurable deterministic signals to be present (volume
    /// confirmation on breakout, volume signature on retest, retest open on
    /// the clean side of the level, retest body strong). Default false.
    /// Requires <see cref="UseRetestValidationAgent"/> = true to have any effect.
    /// </summary>
    public bool UseRetestAiOnlyForMarginal { get; set; } = false;

    /// <summary>
    /// Number of deterministic confirmation signals required for the worker to
    /// classify a retest as "high confidence" and skip the AI validation call.
    /// There are four possible signals (breakout volume passed, retest volume
    /// signature passed, retest opened cleanly above/below the level, retest
    /// body printed in the upper/lower 50% of its own range). Default 3.
    /// </summary>
    public int DeterministicMarginalSignalsRequired { get; set; } = 3;

    /// <summary>
    /// Minimum breakout-candle range as a fraction of the opening range height.
    /// Filters out lifeless wick-style breakouts. 0 disables (default 0.30).
    /// </summary>
    public decimal BreakoutMinRangeFractionOfOpeningRange { get; set; } = 0.30m;

    /// <summary>
    /// Volume confirmation on the breakout candle. The breakout's bar volume must
    /// be at least <c>multiplier × avg(opening-range bar volumes)</c>. Real
    /// breakouts typically print on expanded volume; weak-volume breaks often
    /// fade. 0 disables. Default 1.5 (quality-over-quantity baseline).
    /// </summary>
    public decimal BreakoutVolumeMultiplier { get; set; } = 1.5m;

    /// <summary>
    /// Maximum allowed first-bar range as a fraction of the opening-range midpoint.
    /// Days that open with abnormally wide first-5-min ranges (gaps, news days) are
    /// skipped because the retest geometry doesn't apply cleanly. Applied to BOTH
    /// backtest and live. 0 disables.
    /// </summary>
    public decimal MaxOpeningRangeFractionOfPrice { get; set; } = 0.015m;

    /// <summary>
    /// Maximum age (in seconds) of the cached quote used to decide live entry.
    /// When the cached quote is older than this, the worker forces a fresh HTTP
    /// quote fetch before sizing/entering. 0 disables the freshness check.
    /// Default 10s.
    /// </summary>
    public int MaxQuoteStalenessSeconds { get; set; } = 10;

    /// <summary>
    /// Hard guard against chasing: reject the trade if the live quote has drifted
    /// from the retest bar's close by more than this fraction. Prevents entering
    /// a setup whose context has already moved away from the breakout-retest
    /// geometry. Default 0.005 = 50 bps. 0 disables the guard.
    /// </summary>
    public decimal MaxEntrySlippageFromRetestFraction { get; set; } = 0.005m;

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
    /// Quality filter on stop width relative to the symbol's daily ATR(14).
    /// Rejects the trade when the planned stop distance exceeds
    /// <c>multiplier × dailyATR</c>. Setups with stops wider than ~2.5 daily ATRs
    /// typically have poor reward/risk because the take-profit at 2:1 lives 5
    /// ATRs above entry and rarely hits intraday. When ATR data is unavailable
    /// the check is skipped (does not silently reject). 0 disables. Default 2.5.
    /// </summary>
    public decimal MaxStopAtrMultiple { get; set; } = 2.5m;

    /// <summary>
    /// Minutes before the regular-session close at which the live worker
    /// force-flats any still-open positions tied to its watch states (cancels
    /// resting exit orders and submits market exits). Mirrors the backtest
    /// <see cref="EndOfDayExitBufferMinutes"/> safety. 0 disables. Default 5.
    /// </summary>
    public int LiveEndOfDayExitBufferMinutes { get; set; } = 5;

    /// <summary>
    /// When &gt; 0, the stop is moved to entry once unrealized PnL reaches this many R.
    /// Disabled by default because it caps winners that briefly touch +1R then retrace
    /// before reaching the +2R take-profit. Enable when the trade-off is worth it for
    /// your watchlist's chop profile.
    /// </summary>
    public decimal BreakEvenAtRMultiple { get; set; } = 0m;

    /// <summary>
    /// When &gt; 0, force-flat exit after this many post-entry 1-minute bars when
    /// neither the stop nor take-profit has hit. Disabled by default because it
    /// converts in-progress winners into mid-R exits and distorts the R-multiple
    /// distribution. Enable when you'd rather free capital than wait for resolution.
    /// </summary>
    public int MaxBarsInTradeBeforeFlatExit { get; set; } = 0;

    /// <summary>
    /// Maximum trade entries per trading day. Applied to BOTH backtest and live;
    /// the live worker counts every order it submits (regardless of outcome) and
    /// stops opening new positions once the cap is reached. 0 disables (default 3).
    /// </summary>
    public int MaxTradesPerDay { get; set; } = 3;

    /// <summary>
    /// Daily realized-PnL drawdown limit as a fraction of starting equity.
    /// Backtest uses <see cref="BacktestStartingEquity"/>; live uses the live
    /// account's PortfolioValue (with a fallback to <see cref="BacktestStartingEquity"/>
    /// when the equity query fails). When today's realized PnL drops below
    /// <c>-fraction * equity</c>, the worker stops opening new positions for
    /// the day. 0 disables (default 0.02 = 2%).
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

    // ── Directional indicator filter ─────────────────────────────────────────
    // Applied to both backtests and live trading. When sentiment AI is enabled
    // the AI direction must also pass the indicator check, satisfying the
    // "sentiment AND indicator must agree" requirement.

    /// <summary>
    /// Master switch. When false the indicator filter is bypassed entirely.
    /// </summary>
    public bool UseDirectionalIndicatorFilter { get; set; } = true;

    /// <summary>
    /// One or more indicator modes to evaluate. Each selected mode produces a boolean signal;
    /// <see cref="DirectionalIndicatorRequireAll"/> controls AND vs OR aggregation.
    /// Modes with insufficient bars are skipped (not counted as failures).
    /// Default: Vwap + EmaCross.
    /// </summary>
    public List<DirectionalIndicatorMode> DirectionalIndicatorModes { get; set; } =
        [DirectionalIndicatorMode.Vwap, DirectionalIndicatorMode.EmaCross];

    /// <summary>
    /// When true (AND logic) all enabled indicator modes must agree.
    /// When false (OR logic) any single mode confirming is sufficient.
    /// </summary>
    public bool DirectionalIndicatorRequireAll { get; set; } = true;

    /// <summary>Short EMA period for the EmaCross mode (default 9).</summary>
    public int DirectionalIndicatorEmaShortPeriod { get; set; } = 9;

    /// <summary>Long EMA period for the EmaCross mode (default 20).</summary>
    public int DirectionalIndicatorEmaLongPeriod { get; set; } = 20;

    /// <summary>Lookback period for ADX and DMI smoothing (default 14).</summary>
    public int DirectionalIndicatorAdxPeriod { get; set; } = 14;

    /// <summary>
    /// ADX value below which the market is considered ranging/sidewalk and the trade is skipped (default 25).
    /// </summary>
    public decimal DirectionalIndicatorAdxThreshold { get; set; } = 25m;

    /// <summary>ATR lookback period for the SuperTrend band calculation (default 10).</summary>
    public int DirectionalIndicatorSuperTrendAtrPeriod { get; set; } = 10;

    /// <summary>Multiplier applied to ATR for the SuperTrend bands (default 3.0).</summary>
    public decimal DirectionalIndicatorSuperTrendFactor { get; set; } = 3m;

    public bool LiveUseTrailingStopLoss { get; set; } = false;

    public decimal LivePartialTakeProfitFraction { get; set; } = 0.5m;

    public decimal LiveTrailingStopRiskMultiple { get; set; } = 1.0m;

    public bool LiveTrailingStopBreakEvenProtection { get; set; } = true;

    public string SentimentSystemPrompt { get; set; } =
        "You are a pre-market opportunity selector for an intraday opening-range breakout strategy. Your job is to pick the BEST candidates from a curated watchlist - this is a directional bet, so a wrong-direction signal loses real money. Be SELECTIVE, not inclusive: a watchlist of 30 symbols should normally yield 3-6 strong opportunities, not 15 mediocre ones. Inputs are pre-fetched in the payload: watchlist, Alpha Vantage NEWS_SENTIMENT (do not re-fetch), and prior-day daily candle. Use Alpaca MCP only to verify tradability and (when options are enabled) that both call and put contracts exist in the configured DTE window. A STRONG candidate has (a) a clean directional prior-day candle (close in top/bottom 20% of range, expanded range vs. its 10-day average, above-average volume) and EITHER (b) news sentiment aligned with that direction or (c) no contradictory news at all. Treat conflicting news (positive news on a red daily, or vice-versa) as a STRONG negative. Treat missing or low-relevance news as neutral, not negative. Return between MIN and MAX opportunities; do not pad to MAX if quality candidates do not exist - returning fewer is correct. Use the FULL score range: 85-100 for high-conviction setups with both directional candle and aligned news; 70-84 for valid setups with one clean signal and no conflicting noise; 60-69 for marginal candidates worth a small position. The bot's accepted floor is supplied at runtime (THRESHOLD line below); only return scores below that as explicit exclusions. Return strict JSON only.";

    public string RetestValidationSystemPrompt { get; set; } =
        "You are a quality scorer for intraday breakout-and-retest candidates. The deterministic engine has ALREADY validated geometry, timing, acceptance, pierce/near tolerances, and (when configured) volume signatures - your job is to confirm the candle data is real and to add price-action judgment, NOT to re-enforce those rules. Default to APPROVING the candidate (IsValidRetest=true) unless something clearly looks wrong. Veto only for clear, unambiguous problems you can see in the candles fetched via Alpaca MCP: data gaps or halts in the breakout-to-retest window, the levels in the payload not matching the actual first 5-minute candle, a close back inside the original range between breakout and retest that contradicts the payload, or the retest candle showing decisive failure of the level (close clearly on the wrong side by more than typical noise). Body colour by itself is NOT a veto - a red retest candle that wicks below the level and closes back above is a textbook bullish hold; a green retest candle that wicks above and closes back below is a textbook bearish hold. If a backtest causal cutoff is provided, evaluate only candles at or before it; do not penalize the candidate for confirmation that would only appear after the cutoff. Calibrate your scores so the distribution is meaningful, NOT a rubber stamp: aim for roughly 25% of valid setups scoring 85+, 50% scoring 70-84, and 25% scoring 60-69. The bot's accepted floor is supplied at runtime (THRESHOLD line below); set IsValidRetest=false only when you would genuinely veto and your score is below that floor. Return strict JSON only.";

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
