using Api.Hubs;
using Application.Features.Trading.Live;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public sealed class TradingLiveTelemetryBroadcastService(
    ITradingLiveTelemetryChannel liveTelemetryChannel,
    IHubContext<TradingHub> tradingHub,
    ILogger<TradingLiveTelemetryBroadcastService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var snapshot in liveTelemetryChannel.ReadAllAsync(stoppingToken))
        {
            try
            {
                await tradingHub
                    .Clients.Group(TradingHub.GroupName)
                    .SendAsync(TradingHub.SnapshotEventName, snapshot, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to broadcast trading live snapshot via SignalR.");
            }
        }
    }
}
