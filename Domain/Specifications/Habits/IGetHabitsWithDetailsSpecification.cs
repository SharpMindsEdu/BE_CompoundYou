using Domain.Entities;
using Domain.Repositories;

namespace Domain.Specifications.Habits;

public interface IGetHabitsWithDetailsSpecification : ISpecification<Habit>
{
    IGetHabitsWithDetailsSpecification GetHabitWithTimes();
}
