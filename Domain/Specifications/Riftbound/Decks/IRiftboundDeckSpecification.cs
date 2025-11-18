using Domain.Entities.Riftbound;
using Domain.Repositories;

namespace Domain.Specifications.Riftbound.Decks;

public interface IRiftboundDeckSpecification : ISpecification<RiftboundDeck>
{
    IRiftboundDeckSpecification Reset();
    IRiftboundDeckSpecification IncludeDetails();
    IRiftboundDeckSpecification AccessibleForUser(long userId);
    IRiftboundDeckSpecification ByDeckId(long deckId);
    IRiftboundDeckSpecification FilterBy(
        IReadOnlyCollection<long>? legendIds,
        IReadOnlyCollection<string>? colors
    );
    IRiftboundDeckSpecification OrderByNewest();
}
