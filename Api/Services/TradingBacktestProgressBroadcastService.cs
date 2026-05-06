using Api.Hubs;
using Application.Features.Trading.Backtesting;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public sealed class TradingBacktestProgressBroadcastService(
    ITradingBacktestProgressChannel progressChannel,
    IHubContext<TradingHub> tradingHub,
    ILogger<TradingBacktestProgressBroadcastService> logger
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
                    .SendAsync(TradingHub.BacktestProgressEventName, progress, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to broadcast backtest progress via SignalR.");
            }
        }
    }
}
