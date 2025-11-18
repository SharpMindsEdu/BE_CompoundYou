using Application.Features.Riftbound.Cards.Specifications;
using Application.Repositories;
using Domain.Entities.Riftbound;
using Infrastructure.Specifications;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Specifications.Riftbound;

public class RiftboundCardSpecification(IRepository<RiftboundCard> repository)
    : BaseSpecification<RiftboundCard>(repository),
        IRiftboundCardSpecification
{
    public IRiftboundCardSpecification ByFilter(
        int? minCost,
        int? maxCost,
        string? type,
        int? minMight,
        int? maxMight,
        string? setName,
        string? rarity,
        IReadOnlyCollection<string>? colors,
        string? search
    )
    {
        var predicate = PredicateBuilder.New<RiftboundCard>(true);

        if (minCost.HasValue)
            predicate = predicate.And(card => card.Cost >= minCost);
        if (maxCost.HasValue)
            predicate = predicate.And(card => card.Cost <= maxCost);
        if (!string.IsNullOrWhiteSpace(type))
        {
            predicate = predicate.And(card =>
                card.Type != null && card.Type.ToLower().Contains(type.Trim().ToLower())
            );
        }
        if (minMight.HasValue)
            predicate = predicate.And(card => card.Might >= minMight);
        if (maxMight.HasValue)
            predicate = predicate.And(card => card.Might <= maxMight);
        if (!string.IsNullOrWhiteSpace(setName))
        {
            predicate = predicate.And(card =>
                card.SetName != null
                && EF.Functions.ILike(card.SetName, $"%{setName.Trim()}%")
            );
        }
        if (!string.IsNullOrWhiteSpace(rarity))
        {
            predicate = predicate.And(card =>
                card.Rarity != null && card.Rarity.ToLower() == rarity.Trim().ToLower()
            );
        }
        if (colors is not null && colors.Count > 0)
        {
            var normalizedColors = colors
                .Select(c => c.Trim().ToUpperInvariant())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();
            if (normalizedColors.Count > 0)
            {
                predicate = predicate.And(card =>
                    card.Color != null
                    && card.Color.Any(color => normalizedColors.Contains(color.ToUpper()))
                );
            }
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            predicate = predicate.And(card =>
                EF.Functions.ILike(card.Name, $"%{search.Trim()}%")
            );
        }

        ApplyCriteria(predicate);
        return this;
    }
}
