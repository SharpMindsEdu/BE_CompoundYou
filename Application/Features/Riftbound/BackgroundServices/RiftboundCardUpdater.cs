using Domain.Entities.Riftbound;
using Domain.Repositories;
using Domain.Services.Riftbound;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Features.Riftbound.BackgroundServices;

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

                var existingCards = await cardRepository.ListAll(cancellationToken: stoppingToken);
                var existingByReferenceId = existingCards
                    .Where(c => !string.IsNullOrWhiteSpace(c.ReferenceId))
                    .ToDictionary(c => c.ReferenceId, c => c, StringComparer.OrdinalIgnoreCase);

                var incomingReferenceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cardsToInsert = new List<RiftboundCard>();
                var cardsToUpdate = new List<RiftboundCard>();

                foreach (var incoming in cards.Where(c => !string.IsNullOrWhiteSpace(c.ReferenceId)))
                {
                    incoming.ReferenceId = incoming.ReferenceId.Trim();
                    incoming.IsActive = true;
                    incomingReferenceIds.Add(incoming.ReferenceId);

                    if (existingByReferenceId.TryGetValue(incoming.ReferenceId, out var existing))
                    {
                        if (ApplyCardValues(existing, incoming))
                        {
                            cardsToUpdate.Add(existing);
                        }
                    }
                    else
                    {
                        cardsToInsert.Add(incoming);
                    }
                }

                var cardsToDeactivate = existingCards
                    .Where(c =>
                        !string.IsNullOrWhiteSpace(c.ReferenceId)
                        && c.IsActive
                        && !incomingReferenceIds.Contains(c.ReferenceId)
                    )
                    .ToList();
                foreach (var card in cardsToDeactivate)
                {
                    card.IsActive = false;
                }

                if (cardsToInsert.Count > 0)
                {
                    await cardRepository.Add(cardsToInsert.ToArray());
                }

                if (cardsToUpdate.Count > 0 || cardsToDeactivate.Count > 0)
                {
                    cardRepository.Update(cardsToUpdate.Concat(cardsToDeactivate).ToArray());
                }

                if (cardsToInsert.Count > 0 || cardsToUpdate.Count > 0 || cardsToDeactivate.Count > 0)
                {
                    await cardRepository.SaveChanges(stoppingToken);
                }

                logger.LogInformation(
                    "Riftbound-Karten synchronisiert: Inserted={Inserted}, Updated={Updated}, Deactivated={Deactivated}.",
                    cardsToInsert.Count,
                    cardsToUpdate.Count,
                    cardsToDeactivate.Count
                );
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

    private static bool ApplyCardValues(RiftboundCard target, RiftboundCard source)
    {
        var changed = false;

        changed |= SetIfDifferent(target.Slug, source.Slug, value => target.Slug = value);
        changed |= SetIfDifferent(target.Name, source.Name, value => target.Name = value);
        changed |= SetIfDifferent(target.Effect, source.Effect, value => target.Effect = value);
        changed |= SetIfDifferent(target.Cost, source.Cost, value => target.Cost = value);
        changed |= SetIfDifferent(target.Power, source.Power, value => target.Power = value);
        changed |= SetIfDifferent(target.Type, source.Type, value => target.Type = value);
        changed |= SetIfDifferent(target.Supertype, source.Supertype, value => target.Supertype = value);
        changed |= SetIfDifferent(target.Might, source.Might, value => target.Might = value);
        changed |= SetIfDifferent(target.SetName, source.SetName, value => target.SetName = value);
        changed |= SetIfDifferent(target.Rarity, source.Rarity, value => target.Rarity = value);
        changed |= SetIfDifferent(target.Cycle, source.Cycle, value => target.Cycle = value);
        changed |= SetIfDifferent(target.Image, source.Image, value => target.Image = value);
        changed |= SetIfDifferent(target.Promo, source.Promo, value => target.Promo = value);
        changed |= SetIfDifferent(target.IsActive, true, value => target.IsActive = value);

        if (!SequenceEqualIgnoreCase(target.Color, source.Color))
        {
            target.Color = source.Color?.ToList();
            changed = true;
        }

        if (!SequenceEqualIgnoreCase(target.Tags, source.Tags))
        {
            target.Tags = source.Tags?.ToList();
            changed = true;
        }

        if (!SequenceEqualIgnoreCase(target.GameplayKeywords, source.GameplayKeywords))
        {
            target.GameplayKeywords = source.GameplayKeywords?.ToList();
            changed = true;
        }

        return changed;
    }

    private static bool SequenceEqualIgnoreCase(IReadOnlyCollection<string>? left, IReadOnlyCollection<string>? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Count == right.Count
            && left
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(right.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    }

    private static bool SetIfDifferent<T>(T currentValue, T nextValue, Action<T> apply)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, nextValue))
        {
            return false;
        }

        apply(nextValue);
        return true;
    }
}
