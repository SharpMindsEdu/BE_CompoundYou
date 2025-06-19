using Application.Features.Habits.Commands.HabitHistories;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.CommandHandlerTests.HabitHistories;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class CreateHabitHistoryCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateHabitHistory_ShouldCreateEntriesForTodayAndTomorrow()
    {
        var user = new User { DisplayName = "Creator" };
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var habitTimeToday = new HabitTime
        {
            Day = today.DayOfWeek,
            Time = new TimeSpan(7, 0, 0),
            User = user,
            Habit = new Habit { Title = "Test Habit", User = user },
        };

        var habitTimeTomorrow = new HabitTime
        {
            Day = tomorrow.DayOfWeek,
            Time = new TimeSpan(8, 30, 0),
            User = user,
            Habit = new Habit { Title = "Test Habit", User = user },
        };

        PersistWithDatabase(db =>
        {
            db.Add(user);
            db.AddRange(habitTimeToday, habitTimeTomorrow);
        });

        var command = new CreateHabitHistory.CreateHabitHistoryCommand(user.Id);
        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        WithDatabase(db =>
        {
            var histories = db.Set<HabitHistory>().Where(h => h.UserId == user.Id).ToList();
            Assert.Equal(2, histories.Count);
            Assert.Contains(histories, h => h.Date == today + habitTimeToday.Time);
            Assert.Contains(histories, h => h.Date == tomorrow + habitTimeTomorrow.Time);
        });
    }

    [Fact]
    public async Task CreateHabitHistory_ShouldNotDuplicateIfAlreadyExists()
    {
        var user = new User { DisplayName = "NoDuplicate" };
        var today = DateTime.UtcNow.Date;

        var habitTime = new HabitTime
        {
            Day = today.DayOfWeek,
            Time = new TimeSpan(9, 0, 0),
            User = user,
            Habit = new Habit { Title = "Test Habit", User = user },
        };

        var existing = new HabitHistory
        {
            User = user,
            HabitTime = habitTime,
            Habit = habitTime.Habit,
            Date = DateTime.SpecifyKind(today + habitTime.Time, DateTimeKind.Utc),
        };

        PersistWithDatabase(db =>
        {
            db.Add(existing);
        });

        var command = new CreateHabitHistory.CreateHabitHistoryCommand(user.Id);
        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        WithDatabase(db =>
        {
            var histories = db.Set<HabitHistory>().Where(h => h.UserId == user.Id).ToList();
            Assert.Single(histories);
        });
    }

    [Fact]
    public async Task CreateHabitHistory_ShouldFilterByUserId()
    {
        var user1 = new User { DisplayName = "User1" };
        var user2 = new User { DisplayName = "User2" };
        var today = DateTime.UtcNow.Date;

        var habitTimeUser1 = new HabitTime
        {
            Day = today.DayOfWeek,
            Time = new TimeSpan(10, 0, 0),
            User = user1,
            Habit = new Habit { Title = "Test Habit", User = user1 },
        };

        var habitTimeUser2 = new HabitTime
        {
            Day = today.DayOfWeek,
            Time = new TimeSpan(11, 0, 0),
            User = user2,
            Habit = new Habit { Title = "Test Habit", User = user2 },
        };

        PersistWithDatabase(db =>
        {
            db.AddRange(user1, user2);
            db.AddRange(habitTimeUser1, habitTimeUser2);
        });

        var command = new CreateHabitHistory.CreateHabitHistoryCommand(user1.Id);
        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        WithDatabase(db =>
        {
            var all = db.Set<HabitHistory>().ToList();
            Assert.Single(all);
            Assert.Equal(user1.Id, all[0].UserId);
        });
    }

    [Fact]
    public async Task CreateHabitHistory_WithNoMatchingHabitTimes_ShouldSucceedWithNoEntries()
    {
        var user = new User { DisplayName = "EmptyUser" };
        PersistWithDatabase(db => db.Add(user));

        var command = new CreateHabitHistory.CreateHabitHistoryCommand(user.Id);
        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var histories = db.Set<HabitHistory>().Where(h => h.UserId == user.Id).ToList();
            Assert.Empty(histories);
        });
    }

    [Fact]
    public async Task CreateHabitHistory_ShouldCreateOnlyForToday_IfTomorrowHasNoMatchingTimes()
    {
        var user = new User { DisplayName = "TodayOnly" };
        var today = DateTime.UtcNow.Date;
        var todayTime = new HabitTime
        {
            Day = today.DayOfWeek,
            Time = new TimeSpan(7, 0, 0),
            User = user,
            Habit = new Habit { Title = "Test Habit", User = user },
        };

        var unrelatedTime = new HabitTime
        {
            Day = DayOfWeek.Sunday, // won't match if today isn't Sunday
            Time = new TimeSpan(7, 0, 0),
            User = user,
            Habit = new Habit { Title = "Test Habit", User = user },
        };

        PersistWithDatabase(db =>
        {
            db.Add(user);
            db.AddRange(todayTime, unrelatedTime);
        });

        var command = new CreateHabitHistory.CreateHabitHistoryCommand(user.Id);
        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var histories = db.Set<HabitHistory>().Where(h => h.UserId == user.Id).ToList();
            Assert.Single(histories);
            Assert.Equal(today + todayTime.Time, histories[0].Date);
        });
    }

    [Fact]
    public async Task CreateHabitHistory_WithNullUserId_ShouldCreateEntriesForAllUsers()
    {
        var user1 = new User { DisplayName = "AllUser1" };
        var user2 = new User { DisplayName = "AllUser2" };
        var today = DateTime.UtcNow.Date;

        var habitTime1 = new HabitTime
        {
            Day = today.DayOfWeek,
            Time = new TimeSpan(6, 0, 0),
            User = user1,
            Habit = new Habit { Title = "Test Habit", User = user1 },
        };

        var habitTime2 = new HabitTime
        {
            Day = today.DayOfWeek,
            Time = new TimeSpan(8, 0, 0),
            User = user2,
            Habit = new Habit { Title = "Test Habit", User = user2 },
        };

        PersistWithDatabase(db =>
        {
            db.AddRange(user1, user2);
            db.AddRange(habitTime1, habitTime2);
        });

        var command = new CreateHabitHistory.CreateHabitHistoryCommand(null);
        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var histories = db.Set<HabitHistory>().ToList();
            Assert.Equal(2, histories.Count);
            Assert.Contains(histories, h => h.UserId == user1.Id);
            Assert.Contains(histories, h => h.UserId == user2.Id);
        });
    }

    [Fact]
    public async Task CreateHabitHistory_ShouldNotAddDuplicateForSameTimeOnDifferentDays()
    {
        var user = new User { DisplayName = "SameTimeDifferentDay" };
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var habitTimeToday = new HabitTime
        {
            Day = today.DayOfWeek,
            Time = new TimeSpan(6, 0, 0),
            User = user,
            Habit = new Habit { Title = "Test Habit", User = user },
        };

        var habitTimeTomorrow = new HabitTime
        {
            Day = tomorrow.DayOfWeek,
            Time = new TimeSpan(6, 0, 0), // same time, different day
            User = user,
            Habit = new Habit { Title = "Test Habit", User = user },
        };

        PersistWithDatabase(db =>
        {
            db.Add(user);
            db.AddRange(habitTimeToday, habitTimeTomorrow);
        });

        var command = new CreateHabitHistory.CreateHabitHistoryCommand(user.Id);
        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var histories = db.Set<HabitHistory>().Where(h => h.UserId == user.Id).ToList();
            Assert.Equal(2, histories.Count);
            Assert.Contains(histories, h => h.Date == today + habitTimeToday.Time);
            Assert.Contains(histories, h => h.Date == tomorrow + habitTimeTomorrow.Time);
        });
    }
}
