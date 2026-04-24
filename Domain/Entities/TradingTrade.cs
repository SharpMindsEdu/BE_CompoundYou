using Domain.Services.Trading;

namespace Domain.Entities;

public enum TradingTradeStatus
{
    Submitted = 0,
    EntryFilled = 1,
    Closed = 2,
}

public class TradingTrade : TrackedEntity
{
    public long Id { get; set; }

    public required string Symbol { get; set; }

    public TradingDirection Direction { get; set; }

    public TradingTradeStatus Status { get; set; }

    public required string AlpacaOrderId { get; set; }

    public string? AlpacaTakeProfitOrderId { get; set; }

    public string? AlpacaStopLossOrderId { get; set; }

    public string? AlpacaExitOrderId { get; set; }

    public decimal Quantity { get; set; }

    public decimal PlannedEntryPrice { get; set; }

    public decimal PlannedStopLossPrice { get; set; }

    public decimal PlannedTakeProfitPrice { get; set; }

    public decimal PlannedRiskPerUnit { get; set; }

    public decimal? ActualEntryPrice { get; set; }

    public decimal? ActualExitPrice { get; set; }

    public decimal? RealizedProfitLoss { get; set; }

    public decimal? RealizedGrossProfitLoss { get; set; }

    public decimal? RealizedTotalFees { get; set; }

    public decimal? RealizedAlpacaFees { get; set; }

    public decimal? RealizedSpreadCost { get; set; }

    public decimal? RealizedRMultiple { get; set; }

    public string? ExitReason { get; set; }

    public string? AlpacaOrderStatus { get; set; }

    public string? AlpacaExitOrderStatus { get; set; }

    public int? SentimentScore { get; set; }

    public int? RetestScore { get; set; }

    public DateTimeOffset? SignalRetestBarTimestampUtc { get; set; }

    public string? SignalInsightsJson { get; set; }

    public decimal? OpeningRangeHigh { get; set; }

    public decimal? OpeningRangeLow { get; set; }

    public decimal? OptionPlannedEntryPrice { get; set; }

    public decimal? OptionPlannedStopLossPrice { get; set; }

    public decimal? OptionPlannedTakeProfitPrice { get; set; }

    public decimal? OptionPlannedRiskPerUnit { get; set; }

    public string? RetestAttemptsJson { get; set; }

    public string? FeeBreakdownJson { get; set; }

    public DateTimeOffset SubmittedAtUtc { get; set; }

    public DateTimeOffset? EntryFilledAtUtc { get; set; }

    public DateTimeOffset? ExitFilledAtUtc { get; set; }

    public string? AlpacaOrderPayloadJson { get; set; }

    public string? AlpacaExitOrderPayloadJson { get; set; }

    public DateTimeOffset? FeesLastSyncedAtUtc { get; set; }
}
