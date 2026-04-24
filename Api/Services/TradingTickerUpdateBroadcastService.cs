using Api.Hubs;
using Application.Features.Trading.Live;
using Domain.Services.Trading;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public sealed class TradingTickerUpdateBroadcastService(
    ITradingTickerUpdateChannel tickerUpdateChannel,
    ITradingTickerSubscriptionRegistry subscriptionRegistry,
    IHubContext<TradingHub> tradingHub,
    ILogger<TradingTickerUpdateBroadcastService> logger
) : BackgroundService
{
    private readonly Dictionary<TradingTickerSubscription, AggregatedBarState> _aggregates = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var sourceBar in tickerUpdateChannel.ReadAllAsync(stoppingToken))
        {
            try
            {
                await BroadcastAsync(sourceBar, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to broadcast ticker update.");
            }
        }
    }

    private async Task BroadcastAsync(TradingBarSnapshot sourceBar, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceBar.Symbol))
        {
            return;
        }

        var symbol = sourceBar.Symbol.Trim().ToUpperInvariant();
        var activeSubscriptions = subscriptionRegistry.GetActiveSubscriptions(symbol);
        if (activeSubscriptions.Count == 0)
        {
            ClearAggregateStateForSymbol(symbol);
            return;
        }

        PruneAggregateStateForSymbol(symbol, activeSubscriptions);

        foreach (var subscription in activeSubscriptions)
        {
            var groupName = TradingTickerGroupNames.Build(
                subscription.Symbol,
                subscription.Interval.Canonical
            );

            if (subscription.Interval.Duration <= TimeSpan.FromMinutes(1))
            {
                var update = new TradingTickerUpdate(
                    symbol,
                    subscription.Interval.Canonical,
                    true,
                    sourceBar
                );
                await tradingHub
                    .Clients.Group(groupName)
                    .SendAsync(TradingHub.TickerUpdateEventName, update, cancellationToken);
                continue;
            }

            var bucketStart = AlignTimestamp(sourceBar.Timestamp, subscription.Interval.Duration);
            TradingTickerUpdate? closedUpdate = null;

            if (_aggregates.TryGetValue(subscription, out var aggregate))
            {
                if (aggregate.BucketStart != bucketStart)
                {
                    closedUpdate = new TradingTickerUpdate(
                        symbol,
                        subscription.Interval.Canonical,
                        true,
                        aggregate.ToBar(symbol)
                    );
                    aggregate = AggregatedBarState.Start(bucketStart, sourceBar);
                }
                else
                {
                    aggregate = aggregate.Merge(sourceBar);
                }
            }
            else
            {
                aggregate = AggregatedBarState.Start(bucketStart, sourceBar);
            }

            _aggregates[subscription] = aggregate;

            if (closedUpdate is not null)
            {
                await tradingHub
                    .Clients.Group(groupName)
                    .SendAsync(TradingHub.TickerUpdateEventName, closedUpdate, cancellationToken);
            }

            var liveUpdate = new TradingTickerUpdate(
                symbol,
                subscription.Interval.Canonical,
                false,
                aggregate.ToBar(symbol)
            );
            await tradingHub
                .Clients.Group(groupName)
                .SendAsync(TradingHub.TickerUpdateEventName, liveUpdate, cancellationToken);
        }
    }

    private void ClearAggregateStateForSymbol(string symbol)
    {
        foreach (var key in _aggregates.Keys.Where(x =>
                     x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            _aggregates.Remove(key);
        }
    }

    private void PruneAggregateStateForSymbol(
        string symbol,
        IReadOnlyCollection<TradingTickerSubscription> activeSubscriptions
    )
    {
        var activeSet = activeSubscriptions.ToHashSet();
        foreach (var key in _aggregates.Keys.Where(x =>
                     x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)
                     && !activeSet.Contains(x)).ToArray())
        {
            _aggregates.Remove(key);
        }
    }

    private static DateTimeOffset AlignTimestamp(DateTimeOffset timestamp, TimeSpan duration)
    {
        var normalizedDuration = duration <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : duration;
        var utcTicks = timestamp.UtcDateTime.Ticks;
        var bucketTicks = normalizedDuration.Ticks;
        var alignedTicks = utcTicks - (utcTicks % bucketTicks);
        return new DateTimeOffset(new DateTime(alignedTicks, DateTimeKind.Utc));
    }

    private readonly record struct AggregatedBarState(
        DateTimeOffset BucketStart,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        decimal Volume
    )
    {
        public static AggregatedBarState Start(DateTimeOffset bucketStart, TradingBarSnapshot sourceBar)
        {
            return new AggregatedBarState(
                bucketStart,
                sourceBar.Open,
                sourceBar.High,
                sourceBar.Low,
                sourceBar.Close,
                sourceBar.Volume
            );
        }

        public AggregatedBarState Merge(TradingBarSnapshot sourceBar)
        {
            return new AggregatedBarState(
                BucketStart,
                Open,
                Math.Max(High, sourceBar.High),
                Math.Min(Low, sourceBar.Low),
                sourceBar.Close,
                Volume + sourceBar.Volume
            );
        }

        public TradingBarSnapshot ToBar(string symbol)
        {
            return new TradingBarSnapshot(symbol, BucketStart, Open, High, Low, Close, Volume);
        }
    }
}
