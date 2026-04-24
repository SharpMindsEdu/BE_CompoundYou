using Application.Features.Trading.Automation;
using Domain.Services.Trading;

namespace Application.Features.Trading.Live;

public sealed record TradingLiveSnapshot(
    DateTimeOffset GeneratedAtUtc,
    DateOnly? TradingDate,
    bool WorkerEnabled,
    DateTimeOffset? MarketOpenUtc,
    bool MarketIsOpen,
    IReadOnlyCollection<TradingLiveSymbolSnapshot> Symbols
);

public sealed record TradingLiveSymbolSnapshot(
    string Symbol,
    TradingDirection Direction,
    int SentimentScore,
    TradingSignalInsights? SignalInsights,
    string LifecycleState,
    bool OrderPlaced,
    bool EntryFilled,
    bool ExitFilled,
    bool OrderSubmissionRejected,
    string? OrderId,
    DateTimeOffset? OrderSubmittedAtUtc,
    decimal? PlannedEntryPrice,
    decimal? StopLossPrice,
    decimal? TakeProfitPrice,
    OpeningRangeSnapshotDto? OpeningRange,
    DateTimeOffset? BreakoutTimestampUtc,
    DateTimeOffset? LastEvaluatedRetestTimestampUtc,
    DateTimeOffset? EntryFilledAtUtc,
    DateTimeOffset? ExitFilledAtUtc,
    string? TradedInstrumentSymbol,
    string? OptionContractType,
    decimal? OptionStrikePrice,
    DateOnly? OptionExpirationDate,
    string? PendingExitOrderId,
    string? PendingExitReason,
    DateTimeOffset? EntryBarTimestampUtc,
    DateTimeOffset? ExitBarTimestampUtc,
    int? EntryBarIndex,
    int? ExitBarIndex,
    string? LastOrderSubmissionError,
    DateTimeOffset? LastOrderSubmissionFailedAtUtc,
    decimal? LastPrice,
    IReadOnlyCollection<TradingBarSnapshot> SessionBars
);

public sealed record OpeningRangeSnapshotDto(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    decimal Upper,
    decimal Lower
);

public interface ITradingLiveTelemetryChannel
{
    bool TryPublish(TradingLiveSnapshot snapshot);

    TradingLiveSnapshot GetLatest();

    IAsyncEnumerable<TradingLiveSnapshot> ReadAllAsync(
        CancellationToken cancellationToken = default
    );
}
