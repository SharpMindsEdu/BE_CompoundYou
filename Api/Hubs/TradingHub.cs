using Application.Features.Trading.Live;
using Api.Services;
using Domain.Services.Trading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public sealed class TradingHub(
    ITradingLiveTelemetryChannel liveTelemetryChannel,
    ITradingSentimentProgressChannel sentimentProgressChannel,
    ITradingTickerSubscriptionRegistry tickerSubscriptionRegistry
) : Hub
{
    public const string HubRoute = "/tradingHub";
    public const string SnapshotEventName = "TradingLiveSnapshot";
    public const string SentimentProgressEventName = "TradingSentimentProgress";
    public const string GroupName = "trading-live";
    public const string TickerUpdateEventName = "TradingTickerUpdate";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
        await Clients.Caller.SendAsync(SnapshotEventName, liveTelemetryChannel.GetLatest());
        await Clients.Caller.SendAsync(SentimentProgressEventName, sentimentProgressChannel.GetLatest());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        tickerSubscriptionRegistry.RemoveAllSubscriptions(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName);
        await base.OnDisconnectedAsync(exception);
    }

    public Task RequestLatest()
    {
        return Clients.Caller.SendAsync(SnapshotEventName, liveTelemetryChannel.GetLatest());
    }

    public async Task SubscribeTicker(string symbol, string interval)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var parsedInterval = ParseInterval(interval);

        var (added, subscription) = tickerSubscriptionRegistry.AddSubscription(
            Context.ConnectionId,
            normalizedSymbol,
            parsedInterval
        );
        if (!added)
        {
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            TradingTickerGroupNames.Build(subscription.Symbol, subscription.Interval.Canonical)
        );
    }

    public async Task UnsubscribeTicker(string symbol, string interval)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var parsedInterval = ParseInterval(interval);

        var (removed, subscription) = tickerSubscriptionRegistry.RemoveSubscription(
            Context.ConnectionId,
            normalizedSymbol,
            parsedInterval
        );
        if (!removed)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            TradingTickerGroupNames.Build(subscription.Symbol, subscription.Interval.Canonical)
        );
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new HubException("Symbol is required.");
        }

        return symbol.Trim().ToUpperInvariant();
    }

    private static TradingBarInterval ParseInterval(string interval)
    {
        if (!TradingBarIntervalParser.TryParse(interval, out var parsedInterval))
        {
            throw new HubException(
                "Interval is invalid. Use values like '1min', '5min', '15min', '1h', or '1d'."
            );
        }

        return parsedInterval;
    }
}
