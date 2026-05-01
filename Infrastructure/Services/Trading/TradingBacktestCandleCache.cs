using System.Collections.Concurrent;
using Domain.Services.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public interface ITradingBacktestCandleCache
{
    Task<IReadOnlyCollection<TradingBarSnapshot>> GetOrLoadAsync(
        string symbol,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int barsPerSymbol,
        bool useCache,
        Func<CancellationToken, Task<IReadOnlyCollection<TradingBarSnapshot>>> loader,
        CancellationToken cancellationToken = default
    );

    void InvalidateAll();
}

public sealed class TradingBacktestCandleCache : ITradingBacktestCandleCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly ILogger<TradingBacktestCandleCache> _logger;
    private readonly IOptions<TradingAutomationOptions> _options;

    public TradingBacktestCandleCache(
        IOptions<TradingAutomationOptions> options,
        ILogger<TradingBacktestCandleCache> logger
    )
    {
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TradingBarSnapshot>> GetOrLoadAsync(
        string symbol,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int barsPerSymbol,
        bool useCache,
        Func<CancellationToken, Task<IReadOnlyCollection<TradingBarSnapshot>>> loader,
        CancellationToken cancellationToken = default
    )
    {
        var options = _options.Value;
        if (!useCache)
        {
            return await loader(cancellationToken);
        }

        var key = BuildKey(symbol, startUtc, endUtc, barsPerSymbol);
        if (TryGetValidEntry(key, options, out var cachedBars))
        {
            _logger.LogDebug(
                "Backtest candle cache HIT for {Symbol} ({StartUtc} - {EndUtc}).",
                symbol,
                startUtc,
                endUtc
            );
            return cachedBars;
        }

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TryGetValidEntry(key, options, out cachedBars))
            {
                _logger.LogDebug(
                    "Backtest candle cache HIT (after wait) for {Symbol} ({StartUtc} - {EndUtc}).",
                    symbol,
                    startUtc,
                    endUtc
                );
                return cachedBars;
            }

            _logger.LogDebug(
                "Backtest candle cache MISS for {Symbol} ({StartUtc} - {EndUtc}). Loading from provider.",
                symbol,
                startUtc,
                endUtc
            );

            var loadedBars = (await loader(cancellationToken)).OrderBy(x => x.Timestamp).ToArray();
            _entries[key] = new CacheEntry(DateTimeOffset.UtcNow, loadedBars);
            EnforceCapacity(options);
            return loadedBars;
        }
        finally
        {
            gate.Release();
        }
    }

    public void InvalidateAll()
    {
        _entries.Clear();
        foreach (var gate in _locks.Values)
        {
            gate.Dispose();
        }
        _locks.Clear();
    }

    private bool TryGetValidEntry(
        string key,
        TradingAutomationOptions options,
        out IReadOnlyCollection<TradingBarSnapshot> bars
    )
    {
        bars = [];
        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        var ttlMinutes = Math.Clamp(options.BacktestCandleCacheTtlMinutes, 1, 7 * 24 * 60);
        var expiresAt = entry.CachedAtUtc.AddMinutes(ttlMinutes);
        if (DateTimeOffset.UtcNow > expiresAt)
        {
            _entries.TryRemove(key, out _);
            return false;
        }

        bars = entry.Bars;
        return true;
    }

    private void EnforceCapacity(TradingAutomationOptions options)
    {
        var maxEntries = Math.Clamp(options.BacktestCandleCacheMaxEntries, 100, 50_000);
        if (_entries.Count <= maxEntries)
        {
            return;
        }

        var overflow = _entries.Count - maxEntries;
        foreach (var item in _entries
                     .OrderBy(x => x.Value.CachedAtUtc)
                     .Take(overflow))
        {
            _entries.TryRemove(item.Key, out _);
            _locks.TryRemove(item.Key, out var gate);
            gate?.Dispose();
        }
    }

    private static string BuildKey(
        string symbol,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int barsPerSymbol
    )
    {
        return string.Join(
            '|',
            symbol.Trim().ToUpperInvariant(),
            startUtc.ToUnixTimeMilliseconds(),
            endUtc.ToUnixTimeMilliseconds(),
            barsPerSymbol
        );
    }

    private sealed record CacheEntry(
        DateTimeOffset CachedAtUtc,
        IReadOnlyCollection<TradingBarSnapshot> Bars
    );
}
