using Domain.Entities;
using Domain.Repositories;

namespace Domain.Specifications.Habits;

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
