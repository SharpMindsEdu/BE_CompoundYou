using Application.Repositories;
using Domain.Entities;

namespace Application.Features.Habits.Specifications;

public interface ISearchHabitsSpecification : ISpecification<Habit>
{
    ISearchHabitsSpecification AddIncludes();
    ISearchHabitsSpecification ByFilter(
        long userId,
        bool? isPreparationHabit = null,
        int? minScore = null,
        int? maxScore = null,
        string? title = null
    );
}
