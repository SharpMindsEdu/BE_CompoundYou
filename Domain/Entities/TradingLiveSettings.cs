namespace Domain.Entities;

public class TradingLiveSettings
{
    public long Id { get; set; }

    public int? MinOpportunities { get; set; }
    public int? MaxOpportunities { get; set; }
    public int? MinimumSentimentScore { get; set; }
    public int? MinimumRetestScore { get; set; }
    public int? MinimumMinutesFromMarketOpenForEntry { get; set; }
    public int? MaximumMinutesFromMarketOpenForEntry { get; set; }
    public decimal? MinimumEntryDistanceFromRangeFraction { get; set; }
    public int? MaxMinutesBreakoutToRetest { get; set; }
    public int? MinCandlesBetweenBreakoutAndRetest { get; set; }
    public decimal? StopLossBufferFraction { get; set; }
    public decimal? RewardToRiskRatio { get; set; }
    public int? OrderQuantity { get; set; }
    public decimal? RiskPerTradeFraction { get; set; }
    public decimal? BreakEvenAtRMultiple { get; set; }
    public int? MaxBarsInTradeBeforeFlatExit { get; set; }
    public int? MaxTradesPerDay { get; set; }
    public decimal? MaxDailyLossFraction { get; set; }
    public bool? UseTrailingStopLoss { get; set; }
    public decimal? PartialTakeProfitFraction { get; set; }
    public decimal? TrailingStopRiskMultiple { get; set; }
    public bool? TrailingStopBreakEvenProtection { get; set; }
    public bool? UseRetestValidationAgent { get; set; }
    public bool? UseDirectionalIndicatorFilter { get; set; }
    public bool? DirectionalIndicatorRequireAll { get; set; }
    public string? DirectionalIndicatorModesJson { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
