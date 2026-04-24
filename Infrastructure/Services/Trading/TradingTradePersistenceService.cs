using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Features.Trading.Automation;
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
    DateTimeOffset SubmittedAtUtc
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
        CancellationToken cancellationToken = default
    );
}

public sealed class TradingTradePersistenceService : ITradingTradePersistenceService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = false };

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
        trade.RetestScore = submission.RetestScore;
        trade.SignalRetestBarTimestampUtc = submission.SignalRetestBarTimestampUtc;
        trade.SignalInsightsJson = SerializeSignalInsights(submission.SignalInsights);
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

        if (trade.ActualEntryPrice is decimal entry && trade.ActualExitPrice is decimal exit)
        {
            var quantity = trade.Quantity > 0m
                ? trade.Quantity
                : (orderSnapshot.FilledQuantity > 0m ? orderSnapshot.FilledQuantity : orderSnapshot.Quantity);
            if (quantity > 0m)
            {
                var instrumentMultiplier = ResolveInstrumentMultiplier(orderSnapshot.Symbol);
                var perUnitPnl = trade.Direction switch
                {
                    TradingDirection.Bullish => exit - entry,
                    TradingDirection.Bearish => entry - exit,
                    _ => 0m,
                };
                trade.RealizedProfitLoss = decimal.Round(
                    perUnitPnl * quantity * instrumentMultiplier,
                    6
                );

                var totalRisk = trade.PlannedRiskPerUnit > 0m
                    ? trade.PlannedRiskPerUnit * quantity * instrumentMultiplier
                    : 0m;
                if (totalRisk > 0m)
                {
                    trade.RealizedRMultiple = decimal.Round(trade.RealizedProfitLoss.Value / totalRisk, 6);
                }
            }
        }

        trade.UpdatedOn = now;
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
