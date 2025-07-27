using System.Diagnostics;
using Application.Common;
using Application.Features.Habits.Commands;
using Domain.Entities;
using Domain.Enums;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class ExecuteHabitCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ExecuteHabit_WithValidData_ShouldUpdateHistory()
    {
        var user = new User { DisplayName = "Executor" };
        var habit = new Habit() { Title = "Test Habit", User = user };
        var habitTime = new HabitTime { User = user, Habit = habit };
        var history = new HabitHistory
        {
            Date = DateTime.UtcNow,
            IsCompleted = false,
            User = user,
            Habit = habit,
            CreatedByHabitTime = habitTime,
        };
        PersistWithDatabase(db => db.Add(history));

        var command = new ExecuteHabit.ExecuteHabitCommand(history.Id, user.Id, true, "Done it");

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Debug.Assert(result.Data != null);
        Assert.True(result.Data.IsCompleted);
        Assert.Equal("Done it", result.Data.Comment);
    }

    [Fact]
    public async Task ExecuteHabit_WithTooOldDate_ShouldReturnBadRequest()
    {
        var user = new User { DisplayName = "TooLate" };
        var habit = new Habit() { Title = "Test Habit", User = user };
        var habitTime = new HabitTime { User = user, Habit = habit };
        var history = new HabitHistory
        {
            Date = DateTime.UtcNow.AddHours(-25),
            IsCompleted = false,
            User = user,
            Habit = habit,
            CreatedByHabitTime = habitTime,
        };
        PersistWithDatabase(db => db.Add(history));

        var command = new ExecuteHabit.ExecuteHabitCommand(history.Id, user.Id, true, null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task ExecuteHabit_WithFutureDate_ShouldReturnBadRequest()
    {
        var user = new User { DisplayName = "FutureUser" };
        var habit = new Habit() { Title = "Test Habit", User = user };
        var habitTime = new HabitTime { User = user, Habit = habit };
        var history = new HabitHistory
        {
            Date = DateTime.UtcNow.AddHours(1),
            IsCompleted = false,
            User = user,
            Habit = habit,
            CreatedByHabitTime = habitTime,
        };
        PersistWithDatabase(db => db.Add(history));

        var command = new ExecuteHabit.ExecuteHabitCommand(history.Id, user.Id, true, null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task ExecuteHabit_WithInvalidUser_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "Owner" };
        var habit = new Habit() { Title = "Test Habit", User = user };
        var habitTime = new HabitTime { User = user, Habit = habit };
        var history = new HabitHistory
        {
            Date = DateTime.UtcNow,
            IsCompleted = false,
            User = user,
            Habit = habit,
            CreatedByHabitTime = habitTime,
        };
        PersistWithDatabase(db => db.Add(history));

        var command = new ExecuteHabit.ExecuteHabitCommand(history.Id, 999, true, "Hacked");

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ExecuteHabit_WithInvalidId_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "MissingHistory" };
        PersistWithDatabase(db => db.Add(user));

        var command = new ExecuteHabit.ExecuteHabitCommand(9999, user.Id, true, null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ExecuteHabit_WithEmptyComment_ShouldStillSucceed()
    {
        var user = new User { DisplayName = "NoCommenter" };

        var habit = new Habit() { Title = "Test Habit", User = user };
        var habitTime = new HabitTime { User = user, Habit = habit };
        var history = new HabitHistory
        {
            Date = DateTime.UtcNow,
            IsCompleted = true,
            Comment = "Old Comment",
            User = user,
            CreatedByHabitTime = habitTime,
            Habit = habit,
        };
        PersistWithDatabase(db => db.Add(history));

        var command = new ExecuteHabit.ExecuteHabitCommand(history.Id, user.Id, false, "");

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Debug.Assert(result.Data != null);
        Assert.False(result.Data.IsCompleted);
        Assert.Equal("Old Comment", result.Data.Comment); // unchanged
    }

    [Fact]
    public async Task ExecuteHabit_WithTriggeredHabit_ShouldCreateHistory()
    {
        var user = new User { DisplayName = "Triggerer" };
        var baseHabit = new Habit { Title = "Base", User = user };
        var time = new HabitTime { User = user, Habit = baseHabit };
        var history = new HabitHistory
        {
            Date = DateTime.UtcNow,
            User = user,
            Habit = baseHabit,
            CreatedByHabitTime = time,
        };

        var triggeredHabit = new Habit { Title = "Triggered", User = user };
        var trigger = new HabitTrigger
        {
            Habit = triggeredHabit,
            TriggerHabit = baseHabit,
            TriggerHabitId = baseHabit.Id,
            Title = "Chain",
            Type = HabitTriggerType.Trigger,
        };

        PersistWithDatabase(db => db.AddRange(history, trigger));

        var command = new ExecuteHabit.ExecuteHabitCommand(history.Id, user.Id, true, null);
        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        WithDatabase(db =>
        {
            var entry = db.Set<HabitHistory>()
                .FirstOrDefault(h =>
                    h.HabitId == triggeredHabit.Id && h.Date.Date == DateTime.UtcNow.Date
                );
            Assert.NotNull(entry);
            Assert.Null(entry!.HabitTimeId);
        });
    }

    [Fact(Skip = "Require Change")]
    public async Task ExecuteHabit_WithExistingTriggeredHistory_ShouldNotDuplicate()
    {
        var user = new User { DisplayName = "NoDup" };
        var baseHabit = new Habit { Title = "Base", User = user };
        var time = new HabitTime { User = user, Habit = baseHabit };
        var history = new HabitHistory
        {
            Date = DateTime.UtcNow,
            User = user,
            Habit = baseHabit,
            CreatedByHabitTime = time,
        };

        var triggeredHabit = new Habit { Title = "Triggered", User = user };
        var trigger = new HabitTrigger
        {
            Habit = triggeredHabit,
            TriggerHabit = baseHabit,
            TriggerHabitId = baseHabit.Id,
            Title = "Chain",
            Type = HabitTriggerType.Trigger,
        };

        var existing = new HabitHistory
        {
            Habit = triggeredHabit,
            User = user,
            Date = DateTime.UtcNow.Date,
        };

        PersistWithDatabase(db => db.AddRange(history, trigger, existing));

        var command = new ExecuteHabit.ExecuteHabitCommand(history.Id, user.Id, true, null);
        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        WithDatabase(db =>
        {
            var entries = db.Set<HabitHistory>()
                .Where(h => h.HabitId == triggeredHabit.Id)
                .ToList();
            Assert.Single(entries);
        });
    }
}
