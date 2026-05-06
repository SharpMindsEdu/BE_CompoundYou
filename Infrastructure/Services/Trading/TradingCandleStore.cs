using Domain.Services.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Trading;

public interface ITradingCandleStore
{
    Task<IReadOnlyCollection<TradingBarSnapshot>> GetBarsAsync(
        string symbol,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default
    );

    Task SaveBarsAsync(
        string symbol,
        IReadOnlyCollection<TradingBarSnapshot> bars,
        CancellationToken cancellationToken = default
    );
}

public sealed class TradingCandleStore(
    ApplicationDbContext db,
    ILogger<TradingCandleStore> logger
) : ITradingCandleStore
{
    public async Task<IReadOnlyCollection<TradingBarSnapshot>> GetBarsAsync(
        string symbol,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        var bars = await db.TradingCandleBars
            .AsNoTracking()
            .Where(x =>
                x.Symbol == normalizedSymbol
                && x.TimestampUtc >= startUtc
                && x.TimestampUtc <= endUtc
            )
            .OrderBy(x => x.TimestampUtc)
            .Select(x => new TradingBarSnapshot(x.Symbol, x.TimestampUtc, x.Open, x.High, x.Low, x.Close, x.Volume))
            .ToListAsync(cancellationToken);

        if (bars.Count > 0)
        {
            logger.LogDebug(
                "Candle DB HIT: {Count} bars for {Symbol} ({StartUtc} - {EndUtc}).",
                bars.Count,
                normalizedSymbol,
                startUtc,
                endUtc
            );
        }

        return bars;
    }

    public async Task SaveBarsAsync(
        string symbol,
        IReadOnlyCollection<TradingBarSnapshot> bars,
        CancellationToken cancellationToken = default
    )
    {
        if (bars.Count == 0)
            return;

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        var entities = bars.Select(b => new TradingCandleBar
        {
            Symbol = normalizedSymbol,
            TimestampUtc = b.Timestamp,
            Open = b.Open,
            High = b.High,
            Low = b.Low,
            Close = b.Close,
            Volume = b.Volume,
        });

        db.TradingCandleBars.AddRange(entities);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogDebug(
                "Candle DB STORE: saved {Count} bars for {Symbol}.",
                bars.Count,
                normalizedSymbol
            );
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("23505") == true
                                           || ex.InnerException?.Message.Contains("unique") == true)
        {
            // Unique constraint violation from a concurrent insert — ignore, the data is already there.
            db.ChangeTracker.Clear();
            logger.LogDebug(
                "Candle DB STORE: skipped duplicate bars for {Symbol} (concurrent insert).",
                normalizedSymbol
            );
        }
    }
}
