using Domain.Services.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Trading;

public interface ITradingCalendarStore
{
    Task<TradingSessionSnapshot?> GetSessionAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    );

    Task<bool> IsYearSeededAsync(int year, CancellationToken cancellationToken = default);

    Task SaveSessionsAsync(
        IEnumerable<TradingSessionSnapshot> sessions,
        CancellationToken cancellationToken = default
    );
}

public sealed class TradingCalendarStore(
    ApplicationDbContext db,
    ILogger<TradingCalendarStore> logger
) : ITradingCalendarStore
{
    public async Task<TradingSessionSnapshot?> GetSessionAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    )
    {
        var day = await db.TradingCalendarDays
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Date == date, cancellationToken);

        return day is null ? null : new TradingSessionSnapshot(day.Date, day.OpenTimeUtc, day.CloseTimeUtc);
    }

    public async Task<bool> IsYearSeededAsync(int year, CancellationToken cancellationToken = default)
    {
        var jan1 = new DateOnly(year, 1, 1);
        var dec31 = new DateOnly(year, 12, 31);
        return await db.TradingCalendarDays
            .AsNoTracking()
            .AnyAsync(x => x.Date >= jan1 && x.Date <= dec31, cancellationToken);
    }

    public async Task SaveSessionsAsync(
        IEnumerable<TradingSessionSnapshot> sessions,
        CancellationToken cancellationToken = default
    )
    {
        var entities = sessions.Select(s => new TradingCalendarDay
        {
            Date = s.Date,
            OpenTimeUtc = s.OpenTimeUtc,
            CloseTimeUtc = s.CloseTimeUtc,
        }).ToList();

        if (entities.Count == 0)
            return;

        db.TradingCalendarDays.AddRange(entities);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogDebug("Calendar DB STORE: saved {Count} trading days.", entities.Count);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("23505") == true
                                           || ex.InnerException?.Message.Contains("unique") == true)
        {
            db.ChangeTracker.Clear();
            logger.LogDebug("Calendar DB STORE: skipped duplicate sessions (concurrent insert).");
        }
    }
}
