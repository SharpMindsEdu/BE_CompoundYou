using Domain.Entities.Riftbound;
using Domain.Repositories;

namespace Domain.Specifications.Riftbound.Cards;

public interface IRiftboundCardSpecification : ISpecification<RiftboundCard>
{
    IRiftboundCardSpecification ByFilter(
        int? minCost,
        int? maxCost,
        string? type,
        int? minMight,
        int? maxMight,
        string? setName,
        string? rarity,
        IReadOnlyCollection<string>? colors,
        string? search
    );
}
