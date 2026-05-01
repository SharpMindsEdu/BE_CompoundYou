namespace Domain.Services.Trading;

public enum TradingDirection
{
    Bullish = 0,
    Bearish = 1,
}

public enum TradingOrderSide
{
    Buy = 0,
    Sell = 1,
}

public enum TradingOptionType
{
    Call = 0,
    Put = 1,
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

public sealed record TradingMarketOrderRequest(
    string Symbol,
    TradingOrderSide Side,
    decimal Quantity
);

public sealed record TradingEquityStopLossOrderRequest(
    string Symbol,
    TradingOrderSide Side,
    decimal Quantity,
    decimal StopPrice
);

public sealed record TradingEquityTrailingStopOrderRequest(
    string Symbol,
    TradingOrderSide Side,
    decimal Quantity,
    decimal TrailPrice
);

public sealed record TradingOptionOrderRequest(
    string OptionSymbol,
    TradingOrderSide Side,
    int Quantity
);

public sealed record TradingOptionStopLossOrderRequest(
    string OptionSymbol,
    int Quantity,
    decimal StopPrice
);

public sealed record TradingOptionLimitOrderRequest(
    string OptionSymbol,
    TradingOrderSide Side,
    int Quantity,
    decimal LimitPrice
);

public sealed record TradingOptionContractSnapshot(
    string Symbol,
    string UnderlyingSymbol,
    TradingOptionType ContractType,
    DateOnly ExpirationDate,
    decimal StrikePrice,
    decimal? ClosePrice
);

public sealed record TradingOptionQuoteSnapshot(
    string Symbol,
    decimal BidPrice,
    decimal AskPrice,
    decimal LastPrice,
    DateTimeOffset Timestamp
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

public sealed record TradingFeeActivitySnapshot(
    string ActivityId,
    string ActivitySubType,
    string? OrderId,
    DateOnly? ActivityDate,
    DateTimeOffset? CreatedAt,
    decimal NetAmount,
    string Description,
    string Currency
);
