using System.Diagnostics;
using Application.Common;
using Application.Features.Habits.Commands;
using Application.Features.Habits.DTOs;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class UpdateHabitCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task UpdateHabit_ShouldUpdateAndDeleteAndAddTimesCorrectly()
    {
        var user = new User { DisplayName = "ChangeMaster" };
        var habit = new Habit
        {
            Title = "Time Tester",
            Score = 1,
            Description = "Initial",
            Motivation = "Start",
            User = user,
            Times =
            [
                new HabitTime
                {
                    Day = DayOfWeek.Monday,
                    Time = new TimeSpan(7, 0, 0),
                    User = user,
                },
                new HabitTime
                {
                    Day = DayOfWeek.Wednesday,
                    Time = new TimeSpan(12, 0, 0),
                    User = user,
                },
                new HabitTime
                {
                    Day = DayOfWeek.Friday,
                    Time = new TimeSpan(17, 30, 0),
                    User = user,
                },
            ],
        };

        PersistWithDatabase(db => db.Add(habit));
        var mondayId = habit.Times.First(t => t.Day == DayOfWeek.Monday).Id;
        var fridayId = habit.Times.First(t => t.Day == DayOfWeek.Friday).Id;

        var updatedTimes = new List<HabitTimeDto>
        {
            // Update Monday time
            new(mondayId, DayOfWeek.Monday, new TimeSpan(8, 0, 0)),
            // Remove Wednesday (by omitting it)

            // Keep Friday as-is
            new(fridayId, DayOfWeek.Friday, new TimeSpan(17, 30, 0)),
            // Add Sunday new time
            new(0, DayOfWeek.Sunday, new TimeSpan(14, 15, 0)),
        };

        var command = new UpdateHabit.UpdateHabitCommand(
            habit.Id,
            user.Id,
            "Updated Habit",
            20,
            "Updated Desc",
            "Updated Motiv",
            updatedTimes
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Debug.Assert(result.Data != null);
        Debug.Assert(result.Data.Times != null);
        Assert.Equal(3, result.Data.Times.Count);

        // Ensure Monday time is updated
        Assert.Contains(
            result.Data.Times,
            t => t.Day == DayOfWeek.Monday && t.Time == new TimeSpan(8, 0, 0)
        );

        // Ensure Friday remains
        Assert.Contains(
            result.Data.Times,
            t => t.Day == DayOfWeek.Friday && t.Time == new TimeSpan(17, 30, 0)
        );

        // Ensure Sunday was added
        Assert.Contains(
            result.Data.Times,
            t => t.Day == DayOfWeek.Sunday && t.Time == new TimeSpan(14, 15, 0)
        );

        // Ensure Wednesday was removed
        Assert.DoesNotContain(result.Data.Times, t => t.Day == DayOfWeek.Wednesday);
    }

    [Fact]
    public async Task UpdateHabit_WithValidDataAndTimes_ShouldUpdateTimes()
    {
        var user = new User { DisplayName = "Updater" };
        var habit = new Habit
        {
            Title = "Old Habit",
            Score = 20,
            Description = "Desc",
            Motivation = "Motiv",
            User = user,
            Times =
            [
                new()
                {
                    Day = DayOfWeek.Monday,
                    Time = new TimeSpan(8, 0, 0),
                    User = user,
                },
            ],
        };

        PersistWithDatabase(db => db.Add(habit));

        var updatedTimes = new List<HabitTimeDto>
        {
            new(0, DayOfWeek.Tuesday, new TimeSpan(10, 0, 0)),
            new(0, DayOfWeek.Saturday, new TimeSpan(16, 30, 0)),
        };

        var command = new UpdateHabit.UpdateHabitCommand(
            habit.Id,
            user.Id,
            "Updated Habit",
            40,
            "New Description",
            "New Motivation",
            updatedTimes
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Debug.Assert(result.Data != null);
        Debug.Assert(result.Data.Times != null);
        Assert.Equal(2, result.Data.Times.Count);
        Assert.Contains(
            result.Data.Times,
            t => t.Day == DayOfWeek.Tuesday && t.Time == new TimeSpan(10, 0, 0)
        );
        Assert.Contains(
            result.Data.Times,
            t => t.Day == DayOfWeek.Saturday && t.Time == new TimeSpan(16, 30, 0)
        );
    }

    [Fact]
    public async Task UpdateHabit_WithZeroTime_ShouldThrowValidationException()
    {
        var command = new UpdateHabit.UpdateHabitCommand(
            1,
            1,
            "Update Time Fail",
            50,
            null,
            null,
            [new HabitTimeDto(0, DayOfWeek.Monday, TimeSpan.Zero)]
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Time", ex.Message);
    }

    [Fact]
    public async Task UpdateHabit_WithNullTimes_ShouldSucceed()
    {
        var user = new User { DisplayName = "NoTimes" };
        var habit = new Habit
        {
            Title = "WithTimes",
            Score = 10,
            Description = "D",
            Motivation = "M",
            User = user,
            Times =
            [
                new HabitTime
                {
                    Day = DayOfWeek.Monday,
                    Time = new TimeSpan(9, 0, 0),
                    User = user,
                },
            ],
        };

        PersistWithDatabase(db => db.Add(habit));

        var command = new UpdateHabit.UpdateHabitCommand(
            habit.Id,
            user.Id,
            "Without Times",
            10,
            "D",
            "M"
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Debug.Assert(result.Data != null);
        Debug.Assert(result.Data.Times != null);
        Assert.Empty(result.Data.Times);
    }

    [Fact]
    public async Task UpdateHabit_WithValidData_ShouldUpdateHabit()
    {
        var user = new User { DisplayName = "Updater" };
        var habit = new Habit
        {
            Title = "Old Title",
            Score = 10,
            Description = "Old Description",
            Motivation = "Old Motivation",
            User = user,
        };

        PersistWithDatabase(db => db.Add(habit));

        var command = new UpdateHabit.UpdateHabitCommand(
            habit.Id,
            user.Id,
            "New Title",
            99,
            "Updated Description",
            "Updated Motivation"
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("New Title", result.Data.Title);
        Assert.Equal("Updated Description", result.Data.Description);
        Assert.Equal("Updated Motivation", result.Data.Motivation);
        Assert.Equal(99, result.Data.Score);
    }

    [Fact]
    public async Task UpdateHabit_WithNonExistentHabit_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "GhostUser" };
        PersistWithDatabase(db => db.Add(user));

        var command = new UpdateHabit.UpdateHabitCommand(
            9999,
            user.Id,
            "Doesn't Exist",
            10,
            null,
            null
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorResults.EntityNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateHabit_WithNegativeUserId_ShouldThrowValidationException()
    {
        var command = new UpdateHabit.UpdateHabitCommand(1, -1, "Valid Title", 50, null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public async Task UpdateHabit_WithEmptyTitle_ShouldThrowValidationException()
    {
        var command = new UpdateHabit.UpdateHabitCommand(1, 1, "", 50, null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Title", ex.Message);
    }

    [Fact]
    public async Task UpdateHabit_WithTooLongTitle_ShouldThrowValidationException()
    {
        var command = new UpdateHabit.UpdateHabitCommand(1, 1, new string('A', 25), 50, null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Title", ex.Message);
    }

    [Fact]
    public async Task UpdateHabit_WithTooLongDescription_ShouldThrowValidationException()
    {
        var command = new UpdateHabit.UpdateHabitCommand(
            1,
            1,
            "Test",
            50,
            new string('D', 1501),
            null
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Description", ex.Message);
    }

    [Fact]
    public async Task UpdateHabit_WithTooLongMotivation_ShouldThrowValidationException()
    {
        var command = new UpdateHabit.UpdateHabitCommand(
            1,
            1,
            "Test",
            50,
            null,
            new string('M', 421)
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Motivation", ex.Message);
    }

    [Fact]
    public async Task UpdateHabit_WithNegativeId_ShouldThrowValidationException()
    {
        var command = new UpdateHabit.UpdateHabitCommand(-1, 1, "Test", 50, null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Id", ex.Message);
    }

    [Fact]
    public async Task UpdateHabit_RemovesHabitTime_DeletesCorrespondingHabitHistories()
    {
        var user = new User { DisplayName = "RemoveHistories" };
        var mondayTime = new HabitTime
        {
            Day = DateTime.UtcNow.DayOfWeek,
            Time = new TimeSpan(6, 0, 0),
            User = user,
        };
        var habit = new Habit
        {
            Title = "Habit",
            Score = 1,
            Description = "D",
            Motivation = "M",
            User = user,
            Times = [mondayTime],
        };

        var history = new HabitHistory
        {
            Habit = habit,
            User = user,
            Date = DateTime.UtcNow.Date + mondayTime.Time,
            HabitTime = mondayTime,
        };

        PersistWithDatabase(db =>
        {
            db.Add(habit);
            db.Add(history);
        });

        var command = new UpdateHabit.UpdateHabitCommand(
            habit.Id,
            user.Id,
            "Updated",
            2,
            "New",
            "NewMotiv",
            [] // no times → removes all
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Debug.Assert(result.Data != null);
        Debug.Assert(result.Data.Times != null);
        Assert.Empty(result.Data.Times);

        WithDatabase(db =>
            Assert.Empty(db.Set<HabitHistory>().Where(h => h.HabitId == habit.Id).ToList())
        );
    }

    [Fact]
    public async Task UpdateHabit_AddsNewTime_CreatesNewHabitHistories()
    {
        var user = new User { DisplayName = "HistoryCreator" };
        var habit = new Habit
        {
            Title = "New Habit",
            Score = 1,
            Description = "Init",
            Motivation = "Start",
            User = user,
            Times = [],
        };

        PersistWithDatabase(db => db.Add(habit));

        var day = DateTime.UtcNow.DayOfWeek;
        var time = new TimeSpan(7, 0, 0);

        var command = new UpdateHabit.UpdateHabitCommand(
            habit.Id,
            user.Id,
            "Updated Habit",
            10,
            "Updated Desc",
            "Motiv",
            [new HabitTimeDto(0, day, time)]
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Debug.Assert(result.Data != null);
        Debug.Assert(result.Data.Times != null);
        Assert.Single(result.Data.Times);

        var date = DateTime.UtcNow.Date + time;
        WithDatabase(db =>
        {
            var createdHistories = db.Set<HabitHistory>()
                .Where(h => h.HabitId == habit.Id && h.UserId == user.Id && h.Date == date)
                .ToList();
            Assert.Single(createdHistories);
            Assert.Equal(date, createdHistories[0].Date);
        });
    }

    [Fact]
    public async Task UpdateHabit_ChangesTime_DeletesOldHistoryAndCreatesNewOne()
    {
        var user = new User { DisplayName = "TimeChanger" };
        var day = DateTime.UtcNow.DayOfWeek;
        var oldTime = new TimeSpan(6, 0, 0);
        var newTime = new TimeSpan(8, 0, 0);
        var habitTime = new HabitTime
        {
            Day = day,
            Time = oldTime,
            User = user,
        };
        var habit = new Habit
        {
            Title = "Changing",
            Score = 1,
            User = user,
            Times = [habitTime],
        };

        var history = new HabitHistory
        {
            Habit = habit,
            User = user,
            HabitTime = habitTime,
            Date = DateTime.UtcNow.Date + oldTime,
        };

        PersistWithDatabase(db =>
        {
            db.Add(habit);
            db.Add(history);
        });

        var command = new UpdateHabit.UpdateHabitCommand(
            habit.Id,
            user.Id,
            "Updated",
            5,
            "D",
            "M",
            [new HabitTimeDto(habitTime.Id, day, newTime)]
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Debug.Assert(result.Data != null);
        Debug.Assert(result.Data.Times != null);
        Assert.Contains(result.Data.Times, t => t.Time == newTime);

        WithDatabase(db =>
        {
            var histories = db.Set<HabitHistory>().Where(h => h.HabitId == habit.Id).ToList();
            Assert.Single(histories);
            Assert.Equal(DateTime.UtcNow.Date + newTime, histories[0].Date);
        });
    }
}
