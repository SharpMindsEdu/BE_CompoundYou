using Application.Repositories;
using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Decks.Specifications;

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
