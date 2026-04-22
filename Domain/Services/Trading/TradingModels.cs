namespace Domain.Services.Trading;

public enum TradingDirection
{
    Bullish = 0,
    Bearish = 1,
}

public sealed record TradingAccountSnapshot(
    string AccountId,
    string Status,
    decimal Cash,
    decimal BuyingPower,
    decimal PortfolioValue,
    string Currency
);

public sealed record TradingPositionSnapshot(
    string Symbol,
    decimal Quantity,
    decimal MarketValue,
    decimal AverageEntryPrice,
    decimal CurrentPrice,
    decimal UnrealizedProfitLoss
);

public sealed record TradingQuoteSnapshot(
    string Symbol,
    decimal BidPrice,
    decimal AskPrice,
    decimal LastPrice,
    DateTimeOffset Timestamp
);

public sealed record TradingBarSnapshot(
    string Symbol,
    DateTimeOffset Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume
);

public sealed record TradingMarketSnapshot(
    TradingAccountSnapshot Account,
    IReadOnlyCollection<TradingPositionSnapshot> Positions,
    IReadOnlyCollection<TradingQuoteSnapshot> Quotes,
    IReadOnlyDictionary<string, IReadOnlyCollection<TradingBarSnapshot>> BarsBySymbol
);

public sealed record TradingMarketClockSnapshot(
    bool IsOpen,
    DateTimeOffset Timestamp,
    DateTimeOffset NextOpen,
    DateTimeOffset NextClose
);

public sealed record TradingSessionSnapshot(
    DateOnly Date,
    DateTimeOffset OpenTimeUtc,
    DateTimeOffset CloseTimeUtc
);

public sealed record TradingBracketOrderRequest(
    string Symbol,
    TradingDirection Direction,
    decimal Quantity,
    decimal StopLossPrice,
    decimal TakeProfitPrice
);

public sealed record TradingOrderSubmissionResult(
    string OrderId,
    string Symbol,
    string Status,
    string Side,
    decimal Quantity
);

public sealed record TradingOrderSnapshot(
    string OrderId,
    string Symbol,
    string Status,
    string Side,
    string OrderType,
    decimal Quantity,
    decimal FilledQuantity,
    decimal FilledAveragePrice,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? FilledAt,
    DateTimeOffset? CanceledAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyCollection<TradingOrderSnapshot> Legs
);
