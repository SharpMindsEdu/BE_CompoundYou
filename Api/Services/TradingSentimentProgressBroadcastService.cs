using Api.Hubs;
using Application.Features.Trading.Live;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public sealed class TradingSentimentProgressBroadcastService(
    ITradingSentimentProgressChannel progressChannel,
    IHubContext<TradingHub> tradingHub,
    ILogger<TradingSentimentProgressBroadcastService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var progress in progressChannel.ReadAllAsync(stoppingToken))
        {
            try
            {
                await tradingHub
                    .Clients.Group(TradingHub.GroupName)
                    .SendAsync(TradingHub.SentimentProgressEventName, progress, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to broadcast sentiment progress via SignalR.");
            }
        }
    }
}
