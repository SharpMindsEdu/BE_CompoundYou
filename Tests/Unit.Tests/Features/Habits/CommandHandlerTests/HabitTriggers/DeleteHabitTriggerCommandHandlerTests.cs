using Application.Common;
using Application.Features.Habits.Commands.HabitTriggers;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.CommandHandlerTests.HabitTriggers;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class DeleteHabitTriggerCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task DeleteHabitTrigger_WithValidData_ShouldSucceed()
    {
        var user = new User { DisplayName = "Deleter" };
        var habit = new Habit { Title = "Habit", User = user };
        var trigger = new HabitTrigger { Title = "To Delete", Habit = habit };

        PersistWithDatabase(db => db.Add(trigger));

        var command = new DeleteHabitTrigger.DeleteHabitTriggerCommand(
            habit.Id,
            trigger.Id,
            user.Id
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task DeleteHabitTrigger_WithWrongUser_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "WrongUser" };
        var owner = new User { DisplayName = "Owner" };
        var habit = new Habit { Title = "Habit", User = owner };
        var trigger = new HabitTrigger { Title = "Protected", Habit = habit };

        PersistWithDatabase(db => db.AddRange(trigger, user));

        var command = new DeleteHabitTrigger.DeleteHabitTriggerCommand(
            habit.Id,
            trigger.Id,
            user.Id
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(ErrorResults.EntityNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteHabitTrigger_WithWrongHabit_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "User" };
        var habit = new Habit { Title = "Real Habit", User = user };
        var otherHabit = new Habit { Title = "Fake Habit", User = user };
        var trigger = new HabitTrigger { Title = "Trigger", Habit = otherHabit };

        PersistWithDatabase(db => db.AddRange(habit, trigger));

        var command = new DeleteHabitTrigger.DeleteHabitTriggerCommand(
            habit.Id,
            trigger.Id,
            user.Id
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("HabitTrigger not found", result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteHabitTrigger_WithInvalidHabitId_ShouldThrowValidationException()
    {
        var command = new DeleteHabitTrigger.DeleteHabitTriggerCommand(-1, 1, 1);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("HabitId", ex.Message);
    }

    [Fact]
    public async Task DeleteHabitTrigger_WithInvalidTriggerId_ShouldThrowValidationException()
    {
        var command = new DeleteHabitTrigger.DeleteHabitTriggerCommand(1, -5, 1);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("TriggerId", ex.Message);
    }

    [Fact]
    public async Task DeleteHabitTrigger_WithNullUserId_ShouldThrowValidationException()
    {
        var command = new DeleteHabitTrigger.DeleteHabitTriggerCommand(1, 1, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public async Task DeleteHabitTrigger_WithNegativeUserId_ShouldThrowValidationException()
    {
        var command = new DeleteHabitTrigger.DeleteHabitTriggerCommand(1, 1, -1);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }
}
