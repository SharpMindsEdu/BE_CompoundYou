using Application.Features.Habits.Specifications;
using Application.Repositories;
using Domain.Entities;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Specifications.Habits;

public class SearchHabitsSpecification(IRepository<Habit> repository)
    : BaseSpecification<Habit>(repository),
        ISearchHabitsSpecification
{
    public ISearchHabitsSpecification AddIncludes()
    {
        AddInclude(x =>
            x.Include(habit => habit.History).Include(x => x.Times).Include(x => x.Triggers)
        );

        return this;
    }

    public ISearchHabitsSpecification ByFilter(
        long userId,
        bool? isPreparationHabit = null,
        int? minScore = null,
        int? maxScore = null,
        string? title = null
    )
    {
        var query = PredicateBuilder.New<Habit>();
        query = query.And(x => x.UserId == userId);

        if (isPreparationHabit.HasValue)
            query = query.And(x => x.IsPreparationHabit == isPreparationHabit);

        if (minScore.HasValue)
            query = query.And(x => x.Score >= minScore);

        if (maxScore.HasValue)
            query = query.And(x => x.Score <= maxScore);

        if (!string.IsNullOrWhiteSpace(title))
            query = query.And(x => x.TitleSearchVector.Matches(EF.Functions.PlainToTsQuery(title)));

        ApplyCriteria(query);
        AddIncludes();
        return this;
    }
}
