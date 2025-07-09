using Application.Features.Habits.Specifications;
using Application.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Specifications.Habits;

public class GetHabitsWithDetailsSpecification(IRepository<Habit> repository)
    : BaseSpecification<Habit>(repository),
        IGetHabitsWithDetailsSpecification
{
    public IGetHabitsWithDetailsSpecification GetHabitWithTimes()
    {
        AddInclude(x => x.Include(habit => habit.Times));
        return this;
    }
}
