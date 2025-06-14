using System.Diagnostics;
using Application.Common;
using Application.Features.Habits.Commands.HabitTriggers;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.CommandHandlerTests.HabitTriggers;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class UpdateHabitTriggerCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task UpdateHabitTrigger_WithValidData_ShouldSucceed()
    {
        var user = new User { DisplayName = "Updater" };
        var habit = new Habit { Title = "My Habit", User = user };
        var trigger = new HabitTrigger
        {
            Title = "Initial",
            Type = HabitTriggerType.Trigger,
            Habit = habit,
        };

        PersistWithDatabase(db => db.Add(trigger));

        var command = new UpdateHabitTrigger.UpdateHabitTriggerCommand(
            habit.Id,
            trigger.Id,
            user.Id,
            "Updated Title",
            "Updated Description",
            HabitTriggerType.Reward,
            null
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("Updated Title", result.Data?.Title);
        Debug.Assert(result.Data != null);
        Assert.Equal(HabitTriggerType.Reward, result.Data.Type);
    }

    [Fact]
    public async Task UpdateHabitTrigger_WithTriggerHabit_ShouldSucceed()
    {
        var user = new User { DisplayName = "Chainer" };
        var habit = new Habit { Title = "Main", User = user };
        var dependency = new Habit { Title = "Dependency", User = user };
        var trigger = new HabitTrigger
        {
            Title = "Chain",
            Type = HabitTriggerType.Action,
            Habit = habit,
        };

        PersistWithDatabase(db => db.AddRange(trigger, dependency));

        var command = new UpdateHabitTrigger.UpdateHabitTriggerCommand(
            habit.Id,
            trigger.Id,
            user.Id,
            "Chain Updated",
            null,
            HabitTriggerType.Action,
            dependency.Id
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(dependency.Id, result.Data?.TriggerHabitId);
    }

    [Fact]
    public async Task UpdateHabitTrigger_WithWrongUser_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "WrongUser" };
        var otherUser = new User { DisplayName = "Owner" };
        var habit = new Habit { Title = "Protected", User = otherUser };
        var trigger = new HabitTrigger { Title = "Hidden", Habit = habit };

        PersistWithDatabase(db => db.AddRange(trigger, user));

        var command = new UpdateHabitTrigger.UpdateHabitTriggerCommand(
            habit.Id,
            trigger.Id,
            user.Id,
            "Invalid",
            null,
            HabitTriggerType.Craving,
            null
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateHabitTrigger_WithNonexistentTrigger_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "User" };
        var habit = new Habit { Title = "My Habit", User = user };

        PersistWithDatabase(db => db.Add(habit));

        var command = new UpdateHabitTrigger.UpdateHabitTriggerCommand(
            habit.Id,
            9999, // TriggerId existiert nicht
            user.Id,
            "Failing",
            null,
            HabitTriggerType.Craving,
            null
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("HabitTrigger not found", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateHabitTrigger_WithInvalidTriggerHabit_ShouldReturnBadRequest()
    {
        var user = new User { DisplayName = "User" };
        var otherUser = new User { DisplayName = "Other" };
        var habit = new Habit { Title = "Main", User = user };
        var forbidden = new Habit { Title = "OtherHabit", User = otherUser };
        var trigger = new HabitTrigger { Title = "Test", Habit = habit };

        PersistWithDatabase(db => db.AddRange(trigger, forbidden));

        var command = new UpdateHabitTrigger.UpdateHabitTriggerCommand(
            habit.Id,
            trigger.Id,
            user.Id,
            "Failing",
            null,
            HabitTriggerType.Action,
            forbidden.Id
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.BadRequest, result.Status);
        Assert.Equal("Trigger habit not found or unauthorized", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateHabitTrigger_WithEmptyTitle_ShouldThrowValidationException()
    {
        var command = new UpdateHabitTrigger.UpdateHabitTriggerCommand(
            1,
            1,
            1,
            "",
            null,
            HabitTriggerType.Trigger,
            null
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Title", ex.Message);
    }

    [Fact]
    public async Task UpdateHabitTrigger_WithTooLongTitle_ShouldThrowValidationException()
    {
        var command = new UpdateHabitTrigger.UpdateHabitTriggerCommand(
            1,
            1,
            1,
            new string('T', 65),
            null,
            HabitTriggerType.Action,
            null
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Title", ex.Message);
    }

    [Fact]
    public async Task UpdateHabitTrigger_WithTooLongDescription_ShouldThrowValidationException()
    {
        var command = new UpdateHabitTrigger.UpdateHabitTriggerCommand(
            1,
            1,
            1,
            "Valid",
            new string('D', 501),
            HabitTriggerType.Action,
            null
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Description", ex.Message);
    }

    [Fact]
    public async Task UpdateHabitTrigger_WithInvalidEnum_ShouldThrowValidationException()
    {
        var command = new UpdateHabitTrigger.UpdateHabitTriggerCommand(
            1,
            1,
            1,
            "EnumFail",
            null,
            (HabitTriggerType)999,
            null
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Type", ex.Message);
    }
}
