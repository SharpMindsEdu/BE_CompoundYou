using Domain.Services.Trading;

namespace Application.Features.Trading.Backtesting;

public sealed record TradingBacktestRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    string? WatchlistId = null,
    bool? UseTrailingStopLoss = null,
    bool? UseAiSentiment = null,
    bool? UseAiRetestValidation = null,
    int? MinOpportunities = null,
    int? MaxOpportunities = null,
    int? MinimumSentimentScore = null,
    int? MinimumRetestScore = null,
    int? MinimumMinutesFromMarketOpenForEntry = null,
    decimal? MinimumEntryDistanceFromRangeFraction = null,
    bool? AllowOppositeDirectionFallback = null,
    decimal? StartingEquity = null,
    decimal? StopLossBufferFraction = null,
    decimal? RewardToRiskRatio = null,
    decimal? OrderQuantity = null,
    decimal? RiskPerTradeFraction = null,
    bool? UseWholeShareQuantity = null,
    decimal? EstimatedSpreadBps = null,
    decimal? EstimatedSlippageBps = null,
    decimal? MarketOrderSpreadFillRatio = null,
    decimal? CommissionPerUnit = null,
    bool? UseAlpacaStandardFees = null,
    decimal? PartialTakeProfitFraction = null,
    decimal? TrailingStopRiskMultiple = null,
    bool? TrailingStopBreakEvenProtection = null,
    bool? UseCandleCache = null
);

public sealed record TradingBacktestDayResult(
    DateOnly Date,
    int OpportunitiesConsidered,
    int TradesExecuted,
    decimal DayPnl
);

public sealed record TradingBacktestTradeResult(
    DateOnly Date,
    string Symbol,
    TradingDirection Direction,
    decimal Quantity,
    int SentimentScore,
    int RetestScore,
    DateTimeOffset BreakoutTimestampUtc,
    DateTimeOffset RetestTimestampUtc,
    DateTimeOffset EntryFilledAtUtc,
    DateTimeOffset ExitFilledAtUtc,
    int EntryBarIndex,
    int ExitBarIndex,
    int BarsOpen,
    decimal OpenDurationMinutes,
    decimal OpeningRangeHigh,
    decimal OpeningRangeLow,
    decimal EntryPrice,
    decimal StopLossPrice,
    decimal TakeProfitPrice,
    decimal? TrailingStopPrice,
    decimal PartialTakeProfitQuantity,
    decimal RunnerExitQuantity,
    decimal ExitPrice,
    decimal GrossProfitLoss,
    decimal Commissions,
    decimal ProfitLoss,
    decimal RMultiple,
    string ExitReason
);

public sealed record TradingBacktestResult(
    DateOnly StartDate,
    DateOnly EndDate,
    string WatchlistId,
    bool TrailingStopLossEnabled,
    bool SentimentAiEnabled,
    bool RetestValidationAiEnabled,
    int CalendarDays,
    int TradingDaysEvaluated,
    int TotalTrades,
    int Wins,
    int Losses,
    int FlatExits,
    decimal TotalPnl,
    decimal AveragePnlPerTrade,
    decimal WinRatePercent,
    IReadOnlyCollection<TradingBacktestDayResult> Days,
    IReadOnlyCollection<TradingBacktestTradeResult> Trades
);

public interface ITradingBacktestService
{
    Task<TradingBacktestResult> RunAsync(
        TradingBacktestRequest request,
        CancellationToken cancellationToken = default
    );
}
