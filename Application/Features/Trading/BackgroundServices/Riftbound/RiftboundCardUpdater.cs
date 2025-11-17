using Application.Repositories;
using Domain.Entities.Riftbound;
using Domain.Services.Riftbound;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Features.Trading.BackgroundServices.Riftbound;

public class RiftboundCardUpdater(
    IServiceScopeFactory factory,
    ILogger<RiftboundCardUpdater> logger
) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = factory.CreateScope();
        var cardService = scope.ServiceProvider.GetRequiredService<IRiftboundCardService>();
        var cardRepository = scope.ServiceProvider.GetRequiredService<IRepository<RiftboundCard>>();
        try
        {
            logger.LogInformation(
                "RiftboundCardRefreshService gestartet, erster Fetch in {Delay} Sekunden.",
                InitialDelay.TotalSeconds
            );

            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Starte täglichen Riftbound-Karten-Fetch ...");

                var cards = await cardService.GetCardsAsync(stoppingToken);

                logger.LogInformation(
                    "Riftbound-Karten erfolgreich geladen. Anzahl: {Count}",
                    cards.Count
                );

                var existingReferenceIds = await cardRepository.ListAll(
                    selector: c => c.ReferenceId,
                    cancellationToken: stoppingToken
                );

                var newCards = cards
                    .Where(c => !string.IsNullOrWhiteSpace(c.ReferenceId))
                    .Where(c => !existingReferenceIds.Contains(c.ReferenceId!))
                    .ToList();

                if (newCards.Any())
                {
                    logger.LogInformation(
                        "Neue Riftbound-Karten gefunden: {NewCount}",
                        newCards.Count
                    );

                    await cardRepository.Add(newCards.ToArray());
                    await cardRepository.SaveChanges(stoppingToken);

                    logger.LogInformation(
                        "{InsertedCount} neue Riftbound-Karten in die Datenbank eingefügt.",
                        newCards.Count
                    );
                }
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation("RiftboundCardRefreshService wurde abgebrochen.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fehler beim Abrufen der Riftbound-Karten im täglichen Fetch.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation(
                    "RiftboundCardRefreshService wurde während der Wartezeit beendet."
                );
                break;
            }
        }
    }
}
