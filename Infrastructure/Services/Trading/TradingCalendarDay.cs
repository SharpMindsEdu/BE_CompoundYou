namespace Infrastructure.Services.Trading;

public sealed class TradingCalendarDay
{
    public long Id { get; set; }

    public DateOnly Date { get; set; }

    public DateTimeOffset OpenTimeUtc { get; set; }

    public DateTimeOffset CloseTimeUtc { get; set; }
}
