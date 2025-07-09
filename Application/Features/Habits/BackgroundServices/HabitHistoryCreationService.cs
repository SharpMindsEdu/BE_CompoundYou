using Application.Features.Habits.Commands.HabitHistories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Features.Habits.BackgroundServices;

public class HabitHistoryCreationService(
    ILogger<HabitHistoryCreationService> logger,
    IServiceProvider serviceProvider
) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HabitHistoryCreationService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await AddHistoryEntries(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    public async Task AddHistoryEntries(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(
                new CreateHabitHistory.CreateHabitHistoryCommand(null),
                stoppingToken
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while generating HabitHistories");
        }
    }
}
