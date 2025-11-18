using Application.Repositories;
using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Cards.Specifications;

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
