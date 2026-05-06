namespace Infrastructure.Services.Trading;

public sealed class TradingCandleBar
{
    public long Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public decimal Volume { get; set; }
}
