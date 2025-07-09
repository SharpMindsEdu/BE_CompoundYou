using Application.Features.Habits.BackgroundServices;
using Application.Repositories;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.Services;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class HabitHistoryCreationServiceTests : FeatureTestBase
{
    private readonly DayOfWeek _tomorrowDayOfWeek = DateTime.UtcNow.AddDays(1).DayOfWeek;
    private readonly DateTime _tomorrow = DateTime.UtcNow.Date.AddDays(1);

    public HabitHistoryCreationServiceTests(
        PostgreSqlRepositoryTestDatabaseFixture fixture,
        ITestOutputHelper outputHelper
    )
        : base(fixture, outputHelper)
    {
        Services.AddScoped<HabitHistoryCreationService>();
    }

    [Fact]
    public async Task AddHistoryEntries_ShouldCreateEntriesForMatchingHabitTimes()
    {
        var user = new User { DisplayName = "AutoGen" };
        var habit = new Habit
        {
            Title = "Drink Water",
            Score = 1,
            User = user,
        };
        var time1 = new HabitTime
        {
            Day = _tomorrowDayOfWeek,
            Time = new TimeSpan(6, 0, 0),
            Habit = habit,
            User = user,
        };
        var time2 = new HabitTime
        {
            Day = _tomorrowDayOfWeek,
            Time = new TimeSpan(20, 0, 0),
            Habit = habit,
            User = user,
        };

        var expectedDate1 = DateTime.SpecifyKind(_tomorrow + time1.Time, DateTimeKind.Utc);
        var expectedDate2 = DateTime.SpecifyKind(_tomorrow + time2.Time, DateTimeKind.Utc);

        PersistWithDatabase(db => db.AddRange(time1, time2));

        var service = ServiceProvider.GetRequiredService<HabitHistoryCreationService>();
        var repo = ServiceProvider.GetRequiredService<IRepository<HabitHistory>>();

        await service.AddHistoryEntries(CancellationToken.None);

        var histories = await repo.ListAll(cancellationToken: CancellationToken.None);

        Assert.Equal(2, histories.Count);
        Assert.Contains(histories, h => h.Date == expectedDate1);
        Assert.Contains(histories, h => h.Date == expectedDate2);
    }

    [Fact]
    public async Task AddHistoryEntries_ShouldNotDuplicateExistingEntries()
    {
        var user = new User { DisplayName = "DuplicateCheck" };
        var habit = new Habit
        {
            Title = "Read Book",
            Score = 1,
            User = user,
        };
        var time = new HabitTime
        {
            Day = _tomorrowDayOfWeek,
            Time = new TimeSpan(8, 0, 0),
            Habit = habit,
            User = user,
        };

        PersistWithDatabase(db =>
        {
            db.AddRange(time);
            db.SaveChanges();
            db.Add(
                new HabitHistory
                {
                    HabitId = habit.Id,
                    UserId = user.Id,
                    Date = _tomorrow + time.Time,
                    HabitTimeId = time.Id,
                }
            );
        });

        var service = ServiceProvider.GetRequiredService<HabitHistoryCreationService>();
        var repo = ServiceProvider.GetRequiredService<IRepository<HabitHistory>>();

        await service.AddHistoryEntries(CancellationToken.None);

        var histories = await repo.ListAll(h => h.UserId == user.Id, CancellationToken.None);

        Assert.Single(histories); // No new entry should be added
    }

    [Fact]
    public async Task AddHistoryEntries_WithNoMatchingHabitTimes_ShouldDoNothing()
    {
        var user = new User { DisplayName = "NoMatch" };
        var habit = new Habit
        {
            Title = "No Day Match",
            Score = 1,
            User = user,
        };
        var time = new HabitTime
        {
            Day = DateTime.UtcNow.AddDays(-2).DayOfWeek,
            Time = new TimeSpan(6, 0, 0),
            Habit = habit,
            User = user,
        };

        PersistWithDatabase(db => db.AddRange(time));

        var service = ServiceProvider.GetRequiredService<HabitHistoryCreationService>();
        var repo = ServiceProvider.GetRequiredService<IRepository<HabitHistory>>();

        await service.AddHistoryEntries(CancellationToken.None);

        var histories = await repo.ListAll(h => h.UserId == user.Id, CancellationToken.None);

        Assert.Empty(histories);
    }
}
