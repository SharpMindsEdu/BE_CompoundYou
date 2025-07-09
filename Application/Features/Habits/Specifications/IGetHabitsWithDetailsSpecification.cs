using Application.Repositories;
using Domain.Entities;

namespace Application.Features.Habits.Specifications;

public interface IGetHabitsWithDetailsSpecification : ISpecification<Habit>
{
    IGetHabitsWithDetailsSpecification GetHabitWithTimes();
}
