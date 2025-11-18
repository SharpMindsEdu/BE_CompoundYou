using Domain.Entities;
using Domain.Repositories;
using Domain.Specifications.Habits;
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
