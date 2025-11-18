using Application.Features.Riftbound.Decks.Specifications;
using Application.Repositories;
using Domain.Entities.Riftbound;
using Infrastructure.Specifications;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Infrastructure.Specifications.Riftbound;

public class RiftboundDeckSpecification(IRepository<RiftboundDeck> repository)
    : BaseSpecification<RiftboundDeck>(repository),
        IRiftboundDeckSpecification
{
    private ExpressionStarter<RiftboundDeck> _criteria = PredicateBuilder.New<RiftboundDeck>(true);

    public IRiftboundDeckSpecification Reset()
    {
        _criteria = PredicateBuilder.New<RiftboundDeck>(true);
        Criteria = null;
        IncludeExpressions.Clear();
        return this;
    }

    public IRiftboundDeckSpecification IncludeDetails()
    {
        AddInclude(q => q.Include(d => d.Legend!));
        AddInclude(q => q.Include(d => d.Champion!));
        AddInclude(q => q.Include(d => d.Owner!));
        AddInclude(q => q.Include(d => d.Cards).ThenInclude(card => card.Card!));
        AddInclude(q => q.Include(d => d.Ratings));
        AddInclude(q => q.Include(d => d.Shares));
        AddInclude(q => q.Include(d => d.Comments).ThenInclude(c => c.User!));
        return this;
    }

    public IRiftboundDeckSpecification AccessibleForUser(long userId)
    {
        _criteria = _criteria.And(deck =>
            deck.OwnerId == userId
            || deck.IsPublic
            || deck.Shares.Any(share => share.UserId == userId)
        );
        ApplyCriteria(_criteria);
        return this;
    }

    public IRiftboundDeckSpecification ByDeckId(long deckId)
    {
        _criteria = _criteria.And(deck => deck.Id == deckId);
        ApplyCriteria(_criteria);
        return this;
    }

    public IRiftboundDeckSpecification FilterBy(
        IReadOnlyCollection<long>? legendIds,
        IReadOnlyCollection<string>? colors
    )
    {
        if (legendIds is not null && legendIds.Count > 0)
        {
            _criteria = _criteria.And(deck => legendIds.Contains(deck.LegendId));
        }

        if (colors is not null && colors.Count > 0)
        {
            var normalized = colors
                .Select(c => c.Trim().ToUpperInvariant())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            if (normalized.Count == 1)
            {
                var single = normalized.First();
                _criteria = _criteria.And(deck =>
                    deck.Colors != null
                    && deck.Colors.Any(color => color.ToUpper() == single)
                );
            }
            else if (normalized.Count >= 2)
            {
                var target = normalized.Take(2).ToList();
                _criteria = _criteria.And(deck =>
                    deck.Colors != null
                    && deck.Colors.Count == target.Count
                    && deck.Colors.All(color => target.Contains(color.ToUpper()))
                );
            }
        }

        ApplyCriteria(_criteria);
        return this;
    }

    public IRiftboundDeckSpecification OrderByNewest()
    {
        ApplyOrder(false, deck => deck.CreatedOn);
        return this;
    }
}
