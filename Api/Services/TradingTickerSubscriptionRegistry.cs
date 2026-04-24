using Domain.Services.Trading;

namespace Api.Services;

public readonly record struct TradingTickerSubscription(string Symbol, TradingBarInterval Interval);

public interface ITradingTickerSubscriptionRegistry
{
    (bool Added, TradingTickerSubscription Subscription) AddSubscription(
        string connectionId,
        string symbol,
        TradingBarInterval interval
    );

    (bool Removed, TradingTickerSubscription Subscription) RemoveSubscription(
        string connectionId,
        string symbol,
        TradingBarInterval interval
    );

    IReadOnlyCollection<TradingTickerSubscription> RemoveAllSubscriptions(string connectionId);

    IReadOnlyCollection<TradingTickerSubscription> GetActiveSubscriptions(string symbol);
}

public static class TradingTickerGroupNames
{
    public static string Build(string symbol, string intervalCanonical)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var normalizedInterval = intervalCanonical.Trim().ToLowerInvariant();
        return $"trading-ticker:{normalizedSymbol}:{normalizedInterval}";
    }
}

public sealed class TradingTickerSubscriptionRegistry : ITradingTickerSubscriptionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, HashSet<TradingTickerSubscription>> _connectionSubscriptions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<TradingTickerSubscription, int> _subscriptionCounts = [];

    public (bool Added, TradingTickerSubscription Subscription) AddSubscription(
        string connectionId,
        string symbol,
        TradingBarInterval interval
    )
    {
        var subscription = Normalize(symbol, interval);

        lock (_gate)
        {
            if (!_connectionSubscriptions.TryGetValue(connectionId, out var subscriptions))
            {
                subscriptions = [];
                _connectionSubscriptions[connectionId] = subscriptions;
            }

            if (!subscriptions.Add(subscription))
            {
                return (false, subscription);
            }

            _subscriptionCounts.TryGetValue(subscription, out var currentCount);
            _subscriptionCounts[subscription] = currentCount + 1;

            return (true, subscription);
        }
    }

    public (bool Removed, TradingTickerSubscription Subscription) RemoveSubscription(
        string connectionId,
        string symbol,
        TradingBarInterval interval
    )
    {
        var subscription = Normalize(symbol, interval);

        lock (_gate)
        {
            if (
                !_connectionSubscriptions.TryGetValue(connectionId, out var subscriptions)
                || !subscriptions.Remove(subscription)
            )
            {
                return (false, subscription);
            }

            if (subscriptions.Count == 0)
            {
                _connectionSubscriptions.Remove(connectionId);
            }

            DecrementSubscriptionCount(subscription);
            return (true, subscription);
        }
    }

    public IReadOnlyCollection<TradingTickerSubscription> RemoveAllSubscriptions(string connectionId)
    {
        lock (_gate)
        {
            if (!_connectionSubscriptions.Remove(connectionId, out var subscriptions))
            {
                return [];
            }

            foreach (var subscription in subscriptions)
            {
                DecrementSubscriptionCount(subscription);
            }

            return subscriptions.ToArray();
        }
    }

    public IReadOnlyCollection<TradingTickerSubscription> GetActiveSubscriptions(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return [];
        }

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        lock (_gate)
        {
            return _subscriptionCounts
                .Where(x =>
                    x.Value > 0
                    && x.Key.Symbol.Equals(normalizedSymbol, StringComparison.OrdinalIgnoreCase)
                )
                .Select(x => x.Key)
                .OrderBy(x => x.Interval.Duration)
                .ToArray();
        }
    }

    private void DecrementSubscriptionCount(TradingTickerSubscription subscription)
    {
        if (!_subscriptionCounts.TryGetValue(subscription, out var currentCount))
        {
            return;
        }

        if (currentCount <= 1)
        {
            _subscriptionCounts.Remove(subscription);
            return;
        }

        _subscriptionCounts[subscription] = currentCount - 1;
    }

    private static TradingTickerSubscription Normalize(string symbol, TradingBarInterval interval)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var normalizedInterval = new TradingBarInterval(
            interval.Canonical.Trim().ToLowerInvariant(),
            interval.AlpacaTimeframe.Trim(),
            interval.Duration
        );
        return new TradingTickerSubscription(normalizedSymbol, normalizedInterval);
    }
}
