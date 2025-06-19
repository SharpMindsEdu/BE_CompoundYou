using System.Diagnostics;
using Application.Common;
using Application.Features.Habits.Commands;
using Domain.Entities;
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
            HabitTime = habitTime,
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
            HabitTime = habitTime,
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
            HabitTime = habitTime,
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
            HabitTime = habitTime,
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
            HabitTime = habitTime,
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
}
