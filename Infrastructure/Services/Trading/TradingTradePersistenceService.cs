using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Globalization;
using Application.Features.Trading.Automation;
using Application.Features.Trading.Live;
using Domain.Entities;
using Domain.Services.Trading;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Trading;

public sealed record TradingTradeSubmissionSnapshot(
    string Symbol,
    TradingDirection Direction,
    decimal Quantity,
    decimal PlannedEntryPrice,
    decimal PlannedStopLossPrice,
    decimal PlannedTakeProfitPrice,
    decimal PlannedRiskPerUnit,
    int SentimentScore,
    int RetestScore,
    DateTimeOffset? SignalRetestBarTimestampUtc,
    TradingSignalInsights? SignalInsights,
    DateTimeOffset SubmittedAtUtc,
    decimal? OpeningRangeHigh = null,
    decimal? OpeningRangeLow = null,
    IReadOnlyCollection<TradingLiveRetestAttemptSnapshot>? RetestAttempts = null,
    decimal? OptionPlannedEntryPrice = null,
    decimal? OptionPlannedStopLossPrice = null,
    decimal? OptionPlannedTakeProfitPrice = null,
    decimal? OptionPlannedRiskPerUnit = null,
    long? SentimentAnalysisId = null
);

public interface ITradingTradePersistenceService
{
    Task RecordSubmittedAsync(
        TradingOrderSubmissionResult orderResult,
        TradingTradeSubmissionSnapshot submission,
        TradingOrderSnapshot? orderSnapshot,
        CancellationToken cancellationToken = default
    );

    Task RecordEntryFillAsync(
        string alpacaOrderId,
        TradingOrderSnapshot orderSnapshot,
        CancellationToken cancellationToken = default
    );

    Task RecordExitFillAsync(
        string alpacaOrderId,
        TradingOrderSnapshot orderSnapshot,
        TradingOrderSnapshot exitLeg,
        string exitReason,
        IReadOnlyCollection<TradingFeeActivitySnapshot>? feeActivities = null,
        decimal estimatedSpreadBps = 0m,
        DateTimeOffset? feesSyncedAtUtc = null,
        CancellationToken cancellationToken = default
    );

    Task SyncRecentClosedTradeFeesAsync(
        IReadOnlyCollection<TradingFeeActivitySnapshot> feeActivities,
        decimal estimatedSpreadBps,
        DateTimeOffset syncedAtUtc,
        int lookbackDays = 10,
        CancellationToken cancellationToken = default
    );
}

public sealed class TradingTradePersistenceService : ITradingTradePersistenceService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = false };
    private static readonly Regex FeeUnitsRegex = new(
        @"(?<value>\d+(?:\.\d+)?)\s+(?<unit>contracts?|shares?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    private static readonly JsonSerializerOptions SignalInsightsJsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly ApplicationDbContext _dbContext;

    public TradingTradePersistenceService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordSubmittedAsync(
        TradingOrderSubmissionResult orderResult,
        TradingTradeSubmissionSnapshot submission,
        TradingOrderSnapshot? orderSnapshot,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(orderResult.OrderId))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var trade = await FindOrCreateAsync(orderResult.OrderId.Trim(), now, cancellationToken);
        trade.Symbol = NormalizeSymbol(submission.Symbol);
        trade.Direction = submission.Direction;
        trade.Status = TradingTradeStatus.Submitted;
        trade.Quantity = submission.Quantity > 0m ? submission.Quantity : trade.Quantity;
        trade.PlannedEntryPrice = submission.PlannedEntryPrice;
        trade.PlannedStopLossPrice = submission.PlannedStopLossPrice;
        trade.PlannedTakeProfitPrice = submission.PlannedTakeProfitPrice;
        trade.PlannedRiskPerUnit = submission.PlannedRiskPerUnit;
        trade.SentimentScore = submission.SentimentScore;
        trade.SentimentAnalysisId = submission.SentimentAnalysisId;
        trade.RetestScore = submission.RetestScore;
        trade.SignalRetestBarTimestampUtc = submission.SignalRetestBarTimestampUtc;
        trade.SignalInsightsJson = SerializeSignalInsights(submission.SignalInsights);
        if (submission.OpeningRangeHigh.HasValue)
        {
            trade.OpeningRangeHigh = submission.OpeningRangeHigh.Value;
        }
        if (submission.OpeningRangeLow.HasValue)
        {
            trade.OpeningRangeLow = submission.OpeningRangeLow.Value;
        }
        if (submission.RetestAttempts is not null)
        {
            trade.RetestAttemptsJson = SerializeRetestAttempts(submission.RetestAttempts);
        }
        trade.OptionPlannedEntryPrice = submission.OptionPlannedEntryPrice;
        trade.OptionPlannedStopLossPrice = submission.OptionPlannedStopLossPrice;
        trade.OptionPlannedTakeProfitPrice = submission.OptionPlannedTakeProfitPrice;
        trade.OptionPlannedRiskPerUnit = submission.OptionPlannedRiskPerUnit;
        trade.SubmittedAtUtc = submission.SubmittedAtUtc;
        trade.AlpacaOrderStatus = orderResult.Status;

        if (orderSnapshot is not null)
        {
            ApplySnapshot(trade, orderSnapshot);
            trade.AlpacaOrderPayloadJson = SerializePayload(orderSnapshot);
        }

        trade.UpdatedOn = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordEntryFillAsync(
        string alpacaOrderId,
        TradingOrderSnapshot orderSnapshot,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(alpacaOrderId))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var trade = await FindOrCreateAsync(alpacaOrderId.Trim(), now, cancellationToken);
        ApplySnapshot(trade, orderSnapshot);
        trade.AlpacaOrderPayloadJson = SerializePayload(orderSnapshot);

        if (orderSnapshot.FilledAt is DateTimeOffset filledAt)
        {
            trade.EntryFilledAtUtc = filledAt;
            if (trade.Status != TradingTradeStatus.Closed)
            {
                trade.Status = TradingTradeStatus.EntryFilled;
            }
        }

        if (orderSnapshot.FilledAveragePrice > 0m)
        {
            trade.ActualEntryPrice = orderSnapshot.FilledAveragePrice;
        }

        trade.UpdatedOn = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordExitFillAsync(
        string alpacaOrderId,
        TradingOrderSnapshot orderSnapshot,
        TradingOrderSnapshot exitLeg,
        string exitReason,
        IReadOnlyCollection<TradingFeeActivitySnapshot>? feeActivities = null,
        decimal estimatedSpreadBps = 0m,
        DateTimeOffset? feesSyncedAtUtc = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(alpacaOrderId))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var trade = await FindOrCreateAsync(alpacaOrderId.Trim(), now, cancellationToken);
        ApplySnapshot(trade, orderSnapshot);
        trade.AlpacaOrderPayloadJson = SerializePayload(orderSnapshot);
        trade.AlpacaExitOrderId = string.IsNullOrWhiteSpace(exitLeg.OrderId)
            ? trade.AlpacaExitOrderId
            : exitLeg.OrderId.Trim();
        trade.AlpacaExitOrderStatus = exitLeg.Status;
        trade.ExitReason = exitReason;
        trade.AlpacaExitOrderPayloadJson = SerializePayload(exitLeg);
        trade.Status = TradingTradeStatus.Closed;

        if (orderSnapshot.FilledAveragePrice > 0m && trade.ActualEntryPrice is null)
        {
            trade.ActualEntryPrice = orderSnapshot.FilledAveragePrice;
        }

        if (orderSnapshot.FilledAt is DateTimeOffset entryFilledAtUtc && trade.EntryFilledAtUtc is null)
        {
            trade.EntryFilledAtUtc = entryFilledAtUtc;
        }

        if (exitLeg.FilledAveragePrice > 0m)
        {
            trade.ActualExitPrice = exitLeg.FilledAveragePrice;
        }

        if (exitLeg.FilledAt is DateTimeOffset exitFilledAtUtc)
        {
            trade.ExitFilledAtUtc = exitFilledAtUtc;
        }

        RecomputeProfitAndFees(
            trade,
            orderSnapshot,
            exitLeg,
            feeActivities,
            estimatedSpreadBps,
            feesSyncedAtUtc ?? now
        );

        trade.UpdatedOn = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncRecentClosedTradeFeesAsync(
        IReadOnlyCollection<TradingFeeActivitySnapshot> feeActivities,
        decimal estimatedSpreadBps,
        DateTimeOffset syncedAtUtc,
        int lookbackDays = 10,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedLookbackDays = Math.Clamp(lookbackDays, 1, 30);
        var fromUtc = syncedAtUtc.AddDays(-normalizedLookbackDays);
        var trades = await _dbContext.TradingTrades
            .Where(x =>
                x.Status == TradingTradeStatus.Closed
                && x.SubmittedAtUtc >= fromUtc
                && x.ActualEntryPrice.HasValue
                && x.ActualExitPrice.HasValue
            )
            .ToArrayAsync(cancellationToken);
        if (trades.Length == 0)
        {
            return;
        }

        foreach (var trade in trades)
        {
            var entrySnapshot = DeserializeSnapshot(trade.AlpacaOrderPayloadJson);
            if (entrySnapshot is null)
            {
                continue;
            }

            var exitSnapshot =
                DeserializeSnapshot(trade.AlpacaExitOrderPayloadJson)
                ?? BuildFallbackExitSnapshot(trade, entrySnapshot);
            if (exitSnapshot is null)
            {
                continue;
            }

            RecomputeProfitAndFees(
                trade,
                entrySnapshot,
                exitSnapshot,
                feeActivities,
                estimatedSpreadBps,
                syncedAtUtc
            );
            trade.UpdatedOn = syncedAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TradingTrade> FindOrCreateAsync(
        string alpacaOrderId,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var trade = await _dbContext.TradingTrades.FirstOrDefaultAsync(
            x => x.AlpacaOrderId == alpacaOrderId,
            cancellationToken
        );
        if (trade is not null)
        {
            return trade;
        }

        trade = new TradingTrade
        {
            Symbol = "UNKNOWN",
            Direction = TradingDirection.Bullish,
            Status = TradingTradeStatus.Submitted,
            AlpacaOrderId = alpacaOrderId,
            Quantity = 0m,
            PlannedEntryPrice = 0m,
            PlannedStopLossPrice = 0m,
            PlannedTakeProfitPrice = 0m,
            PlannedRiskPerUnit = 0m,
            SubmittedAtUtc = now,
            CreatedOn = now,
            UpdatedOn = now,
            DeletedOn = DateTimeOffset.MinValue,
        };
        _dbContext.TradingTrades.Add(trade);
        return trade;
    }

    private static void ApplySnapshot(TradingTrade trade, TradingOrderSnapshot orderSnapshot)
    {
        if (!string.IsNullOrWhiteSpace(orderSnapshot.Symbol))
        {
            trade.Symbol = ExtractUnderlyingSymbol(orderSnapshot.Symbol);
        }

        trade.Direction = ParseDirection(orderSnapshot.Side, orderSnapshot.Symbol, trade.Direction);
        trade.AlpacaOrderStatus = orderSnapshot.Status;
        trade.SubmittedAtUtc = orderSnapshot.SubmittedAt ?? trade.SubmittedAtUtc;

        var quantity = orderSnapshot.FilledQuantity > 0m ? orderSnapshot.FilledQuantity : orderSnapshot.Quantity;
        if (quantity > 0m)
        {
            trade.Quantity = quantity;
        }

        foreach (var leg in orderSnapshot.Legs)
        {
            if (string.IsNullOrWhiteSpace(leg.OrderType))
            {
                continue;
            }

            if (
                leg.OrderType.Contains("stop", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(leg.OrderId)
            )
            {
                trade.AlpacaStopLossOrderId = leg.OrderId.Trim();
            }

            if (
                leg.OrderType.Contains("limit", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(leg.OrderId)
            )
            {
                trade.AlpacaTakeProfitOrderId = leg.OrderId.Trim();
            }
        }
    }

    private static TradingDirection ParseDirection(
        string? side,
        string? symbol,
        TradingDirection fallback
    )
    {
        if (TryParseOptionContractType(symbol, out var optionType))
        {
            if (side?.Trim().Equals("buy", StringComparison.OrdinalIgnoreCase) == true)
            {
                return optionType == TradingOptionType.Put
                    ? TradingDirection.Bearish
                    : TradingDirection.Bullish;
            }

            if (side?.Trim().Equals("sell", StringComparison.OrdinalIgnoreCase) == true)
            {
                return optionType == TradingOptionType.Put
                    ? TradingDirection.Bullish
                    : TradingDirection.Bearish;
            }

            return fallback;
        }

        if (string.IsNullOrWhiteSpace(side))
        {
            return fallback;
        }

        return side.Trim().Equals("sell", StringComparison.OrdinalIgnoreCase)
            ? TradingDirection.Bearish
            : TradingDirection.Bullish;
    }

    private static decimal ResolveInstrumentMultiplier(string? symbol)
    {
        return TryParseOptionContractType(symbol, out _) ? 100m : 1m;
    }

    private static bool TryParseOptionContractType(string? symbol, out TradingOptionType optionType)
    {
        optionType = default;
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return false;
        }

        var normalized = symbol.Trim().ToUpperInvariant();
        var rootLength = normalized.Length - 15;
        if (rootLength is < 1 or > 6)
        {
            return false;
        }

        for (var i = rootLength; i < rootLength + 6; i++)
        {
            if (!char.IsDigit(normalized[i]))
            {
                return false;
            }
        }

        for (var i = rootLength + 7; i < normalized.Length; i++)
        {
            if (!char.IsDigit(normalized[i]))
            {
                return false;
            }
        }

        var optionTypeIndex = rootLength + 6;
        var optionTypeToken = normalized[optionTypeIndex];
        if (optionTypeToken == 'C')
        {
            optionType = TradingOptionType.Call;
            return true;
        }

        if (optionTypeToken == 'P')
        {
            optionType = TradingOptionType.Put;
            return true;
        }

        return false;
    }

    private static decimal ResolvePerUnitProfitLoss(
        decimal entryPrice,
        decimal exitPrice,
        string? entrySide,
        TradingDirection fallbackDirection
    )
    {
        if (!string.IsNullOrWhiteSpace(entrySide))
        {
            if (entrySide.Trim().Equals("sell", StringComparison.OrdinalIgnoreCase))
            {
                return entryPrice - exitPrice;
            }

            if (entrySide.Trim().Equals("buy", StringComparison.OrdinalIgnoreCase))
            {
                return exitPrice - entryPrice;
            }
        }

        return fallbackDirection switch
        {
            TradingDirection.Bullish => exitPrice - entryPrice,
            TradingDirection.Bearish => entryPrice - exitPrice,
            _ => 0m,
        };
    }

    private static void RecomputeProfitAndFees(
        TradingTrade trade,
        TradingOrderSnapshot entryOrder,
        TradingOrderSnapshot exitOrder,
        IReadOnlyCollection<TradingFeeActivitySnapshot>? feeActivities,
        decimal estimatedSpreadBps,
        DateTimeOffset feesSyncedAtUtc
    )
    {
        if (trade.ActualEntryPrice is not decimal entryPrice || trade.ActualExitPrice is not decimal exitPrice)
        {
            return;
        }

        var quantity = trade.Quantity > 0m
            ? trade.Quantity
            : (entryOrder.FilledQuantity > 0m ? entryOrder.FilledQuantity : entryOrder.Quantity);
        if (quantity <= 0m)
        {
            return;
        }

        var instrumentSymbol = !string.IsNullOrWhiteSpace(entryOrder.Symbol)
            ? entryOrder.Symbol
            : exitOrder.Symbol;
        var instrumentMultiplier = ResolveInstrumentMultiplier(instrumentSymbol);
        var perUnitPnl = ResolvePerUnitProfitLoss(
            entryPrice,
            exitPrice,
            entryOrder.Side,
            trade.Direction
        );
        var grossProfitLoss = decimal.Round(perUnitPnl * quantity * instrumentMultiplier, 6);
        var spreadCost = ResolveEstimatedSpreadCost(
            entryPrice,
            exitPrice,
            quantity,
            instrumentMultiplier,
            estimatedSpreadBps
        );

        var feeBreakdown = ResolveAlpacaFeeBreakdown(
            feeActivities,
            trade,
            entryOrder,
            exitOrder
        );
        var totalFees = decimal.Round(spreadCost + feeBreakdown.TotalCost, 6);
        var netProfitLoss = decimal.Round(grossProfitLoss - totalFees, 6);

        trade.RealizedGrossProfitLoss = grossProfitLoss;
        trade.RealizedAlpacaFees = feeBreakdown.TotalCost;
        trade.RealizedSpreadCost = spreadCost;
        trade.RealizedTotalFees = totalFees;
        trade.RealizedProfitLoss = netProfitLoss;
        trade.FeeBreakdownJson = SerializeFeeBreakdown(
            new PersistedFeeBreakdown(
                estimatedSpreadBps,
                grossProfitLoss,
                spreadCost,
                feeBreakdown.DirectCost,
                feeBreakdown.AllocatedCost,
                feeBreakdown.TotalCost,
                feeBreakdown.DirectActivities,
                feeBreakdown.AllocatedActivities
            )
        );
        trade.FeesLastSyncedAtUtc = feesSyncedAtUtc;

        var totalRisk = trade.PlannedRiskPerUnit > 0m
            ? trade.PlannedRiskPerUnit * quantity * instrumentMultiplier
            : 0m;
        if (totalRisk > 0m)
        {
            trade.RealizedRMultiple = decimal.Round(netProfitLoss / totalRisk, 6);
        }
        else
        {
            trade.RealizedRMultiple = null;
        }
    }

    private static decimal ResolveEstimatedSpreadCost(
        decimal entryPrice,
        decimal exitPrice,
        decimal quantity,
        decimal instrumentMultiplier,
        decimal estimatedSpreadBps
    )
    {
        var normalizedSpreadBps = Math.Max(0m, estimatedSpreadBps);
        if (normalizedSpreadBps <= 0m)
        {
            return 0m;
        }

        var spreadRate = normalizedSpreadBps / 10000m;
        var entryNotional = Math.Abs(entryPrice) * quantity * instrumentMultiplier;
        var exitNotional = Math.Abs(exitPrice) * quantity * instrumentMultiplier;
        var cost = ((entryNotional + exitNotional) * spreadRate) / 2m;
        return decimal.Round(Math.Max(0m, cost), 6);
    }

    private static FeeBreakdownResolution ResolveAlpacaFeeBreakdown(
        IReadOnlyCollection<TradingFeeActivitySnapshot>? feeActivities,
        TradingTrade trade,
        TradingOrderSnapshot entryOrder,
        TradingOrderSnapshot exitOrder
    )
    {
        if (feeActivities is null || feeActivities.Count == 0)
        {
            return FeeBreakdownResolution.Empty;
        }

        var relevantDate = DateOnly.FromDateTime(
            (trade.ExitFilledAtUtc ?? exitOrder.FilledAt ?? trade.SubmittedAtUtc).UtcDateTime
        );
        var orderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(entryOrder.OrderId))
        {
            orderIds.Add(entryOrder.OrderId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(exitOrder.OrderId))
        {
            orderIds.Add(exitOrder.OrderId.Trim());
        }

        var directActivities = feeActivities
            .Where(activity =>
                !string.IsNullOrWhiteSpace(activity.OrderId)
                && orderIds.Contains(activity.OrderId!.Trim())
            )
            .Select(activity => new PersistedFeeActivity(
                activity.ActivityId,
                activity.ActivitySubType,
                activity.OrderId,
                activity.ActivityDate?.ToString("yyyy-MM-dd"),
                activity.Description,
                activity.NetAmount,
                decimal.Round(NetAmountToCost(activity.NetAmount), 6),
                1m
            ))
            .ToArray();
        var directCost = decimal.Round(
            directActivities.Sum(activity => activity.AllocatedCost),
            6
        );

        var isOptionTrade = TryParseOptionContractType(entryOrder.Symbol, out _);
        var tradeUnits = Math.Max(0m, (entryOrder.FilledQuantity > 0m ? entryOrder.FilledQuantity : entryOrder.Quantity))
            + Math.Max(0m, (exitOrder.FilledQuantity > 0m ? exitOrder.FilledQuantity : exitOrder.Quantity));

        var allocatedActivities = new List<PersistedFeeActivity>();
        var unmatchedActivities = feeActivities.Where(activity =>
            string.IsNullOrWhiteSpace(activity.OrderId)
            && activity.ActivityDate == relevantDate
            && IsFeeRelevantToInstrument(activity, isOptionTrade)
        );

        foreach (var activity in unmatchedActivities)
        {
            if (!TryParseFeeUnits(activity.Description, out var feeUnits, out var unitType))
            {
                continue;
            }

            if (feeUnits <= 0m || tradeUnits <= 0m)
            {
                continue;
            }

            var feeIsOption = unitType.Equals("contract", StringComparison.OrdinalIgnoreCase);
            if (feeIsOption != isOptionTrade)
            {
                continue;
            }

            var allocationRatio = Math.Min(1m, tradeUnits / feeUnits);
            if (allocationRatio <= 0m)
            {
                continue;
            }

            var allocatedCost = decimal.Round(
                NetAmountToCost(activity.NetAmount) * allocationRatio,
                6
            );
            allocatedActivities.Add(
                new PersistedFeeActivity(
                    activity.ActivityId,
                    activity.ActivitySubType,
                    activity.OrderId,
                    activity.ActivityDate?.ToString("yyyy-MM-dd"),
                    activity.Description,
                    activity.NetAmount,
                    allocatedCost,
                    decimal.Round(allocationRatio, 6)
                )
            );
        }

        var allocatedCostTotal = decimal.Round(
            allocatedActivities.Sum(activity => activity.AllocatedCost),
            6
        );
        return new FeeBreakdownResolution(
            directCost,
            allocatedCostTotal,
            decimal.Round(directCost + allocatedCostTotal, 6),
            directActivities,
            allocatedActivities.ToArray()
        );
    }

    private static bool IsFeeRelevantToInstrument(
        TradingFeeActivitySnapshot activity,
        bool isOptionTrade
    )
    {
        var description = activity.Description ?? string.Empty;
        var subtype = activity.ActivitySubType ?? string.Empty;
        if (isOptionTrade)
        {
            return description.Contains("OPT", StringComparison.OrdinalIgnoreCase)
                || description.Contains("contract", StringComparison.OrdinalIgnoreCase)
                || subtype.Equals("ORF", StringComparison.OrdinalIgnoreCase)
                || subtype.Equals("OCC", StringComparison.OrdinalIgnoreCase);
        }

        return description.Contains("share", StringComparison.OrdinalIgnoreCase)
            && !description.Contains("OPT", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseFeeUnits(
        string? description,
        out decimal units,
        out string unitType
    )
    {
        units = 0m;
        unitType = string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var match = FeeUnitsRegex.Match(description);
        if (!match.Success)
        {
            return false;
        }

        if (
            !decimal.TryParse(
                match.Groups["value"].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out units
            )
        )
        {
            return false;
        }

        var rawUnit = match.Groups["unit"].Value.Trim().ToLowerInvariant();
        unitType = rawUnit.StartsWith("contract", StringComparison.Ordinal) ? "contract" : "share";
        return true;
    }

    private static decimal NetAmountToCost(decimal netAmount)
    {
        return -netAmount;
    }

    private static string? SerializeFeeBreakdown(PersistedFeeBreakdown breakdown)
    {
        return JsonSerializer.Serialize(breakdown, SignalInsightsJsonSerializerOptions);
    }

    private static TradingOrderSnapshot? DeserializeSnapshot(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TradingOrderSnapshot>(payloadJson, JsonSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TradingOrderSnapshot? BuildFallbackExitSnapshot(
        TradingTrade trade,
        TradingOrderSnapshot entrySnapshot
    )
    {
        if (trade.ActualExitPrice is not decimal exitPrice)
        {
            return null;
        }

        var quantity = trade.Quantity > 0m
            ? trade.Quantity
            : (entrySnapshot.FilledQuantity > 0m ? entrySnapshot.FilledQuantity : entrySnapshot.Quantity);
        var exitSide = entrySnapshot.Side.Trim().Equals("sell", StringComparison.OrdinalIgnoreCase)
            ? "buy"
            : "sell";

        return new TradingOrderSnapshot(
            trade.AlpacaExitOrderId ?? $"{trade.AlpacaOrderId}-exit",
            entrySnapshot.Symbol,
            trade.AlpacaExitOrderStatus ?? "filled",
            exitSide,
            "market",
            quantity,
            quantity,
            exitPrice,
            trade.ExitFilledAtUtc,
            trade.ExitFilledAtUtc,
            null,
            trade.ExitFilledAtUtc,
            []
        );
    }

    private sealed record FeeBreakdownResolution(
        decimal DirectCost,
        decimal AllocatedCost,
        decimal TotalCost,
        IReadOnlyCollection<PersistedFeeActivity> DirectActivities,
        IReadOnlyCollection<PersistedFeeActivity> AllocatedActivities
    )
    {
        public static FeeBreakdownResolution Empty { get; } = new(
            0m,
            0m,
            0m,
            [],
            []
        );
    }

    private sealed record PersistedFeeBreakdown(
        decimal EstimatedSpreadBps,
        decimal GrossProfitLoss,
        decimal EstimatedSpreadCost,
        decimal DirectAlpacaFees,
        decimal AllocatedAlpacaFees,
        decimal TotalAlpacaFees,
        IReadOnlyCollection<PersistedFeeActivity> DirectFeeActivities,
        IReadOnlyCollection<PersistedFeeActivity> AllocatedFeeActivities
    );

    private sealed record PersistedFeeActivity(
        string ActivityId,
        string ActivitySubType,
        string? OrderId,
        string? Date,
        string Description,
        decimal NetAmount,
        decimal AllocatedCost,
        decimal AllocationRatio
    );

    private static string NormalizeSymbol(string? symbol)
    {
        return string.IsNullOrWhiteSpace(symbol) ? "UNKNOWN" : symbol.Trim().ToUpperInvariant();
    }

    private static string ExtractUnderlyingSymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return "UNKNOWN";

        var normalized = symbol.Trim().ToUpperInvariant();
        var rootLength = normalized.Length - 15;
        if (rootLength is >= 1 and <= 6 && TryParseOptionContractType(normalized, out _))
            return normalized[..rootLength];

        return normalized;
    }

    private static string SerializePayload(TradingOrderSnapshot snapshot)
    {
        return JsonSerializer.Serialize(snapshot, JsonSerializerOptions);
    }

    private static string? SerializeSignalInsights(TradingSignalInsights? insights)
    {
        return HasSignalInsights(insights)
            ? JsonSerializer.Serialize(insights, SignalInsightsJsonSerializerOptions)
            : null;
    }

    private static string? SerializeRetestAttempts(
        IReadOnlyCollection<TradingLiveRetestAttemptSnapshot>? attempts
    )
    {
        if (attempts is null || attempts.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(attempts, SignalInsightsJsonSerializerOptions);
    }

    private static bool HasSignalInsights(TradingSignalInsights? insights)
    {
        return insights is not null
            && (
                !string.IsNullOrWhiteSpace(insights.OptionStrategyBias)
                || insights.SentimentScore.HasValue
                || !string.IsNullOrWhiteSpace(insights.SentimentLabel)
                || insights.SentimentRelevance.HasValue
                || !string.IsNullOrWhiteSpace(insights.SentimentSummary)
                || !string.IsNullOrWhiteSpace(insights.CandleBias)
                || !string.IsNullOrWhiteSpace(insights.CandleSummary)
                || !string.IsNullOrWhiteSpace(insights.Reason)
                || !string.IsNullOrWhiteSpace(insights.RiskNotes)
            );
    }
}
