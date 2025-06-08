using Application.Repositories;
using Domain.Entities;

namespace Application.Specifications.Habits;

public interface ISearchHabitsSpecification : ISpecification<Habit>
{
    public ISearchHabitsSpecification ByFilter(
        long userId,
        bool? isPreparationHabit = null,
        int? minScore = null,
        int? maxScore = null,
        string? title = null
    );
}
