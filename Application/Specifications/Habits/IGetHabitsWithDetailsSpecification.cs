using Application.Repositories;
using Domain.Entities;

namespace Application.Specifications.Habits;

public interface IGetHabitsWithDetailsSpecification : ISpecification<Habit>
{
    IGetHabitsWithDetailsSpecification GetHabitWithTimes();
}
