using Application.Features.Habits.Commands.HabitTriggers;
using Application.Shared;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.CommandHandlerTests.HabitTriggers;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class CreateHabitTriggerCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Theory]
    [InlineData(HabitTriggerType.Trigger)]
    [InlineData(HabitTriggerType.Craving)]
    [InlineData(HabitTriggerType.Action)]
    [InlineData(HabitTriggerType.Reward)]
    public async Task CreateHabitTrigger_WithValidTypes_ShouldSucceed(HabitTriggerType type)
    {
        var user = new User { DisplayName = "EnumUser" };
        var habit = new Habit { Title = "Habit", User = user };

        PersistWithDatabase(db => db.Add(habit));

        var command = new CreateHabitTrigger.CreateHabitTriggerCommand(
            habit.Id,
            user.Id,
            $"Trigger for {type}",
            "Description",
            type,
            null
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(type, result.Data.Type);
        Assert.Equal(habit.Id, result.Data.HabitId);
    }

    [Fact]
    public async Task CreateHabitTrigger_WithTriggerHabit_ShouldSucceed()
    {
        var user = new User { DisplayName = "ChainUser" };
        var mainHabit = new Habit { Title = "Main", User = user };
        var triggerHabit = new Habit { Title = "Trigger", User = user };

        PersistWithDatabase(db => db.AddRange(mainHabit, triggerHabit));

        var command = new CreateHabitTrigger.CreateHabitTriggerCommand(
            mainHabit.Id,
            user.Id,
            "With Dependency",
            null,
            HabitTriggerType.Action,
            triggerHabit.Id
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(triggerHabit.Id, result.Data?.TriggerHabitId);
    }

    [Fact]
    public async Task CreateHabitTrigger_WithNonexistentHabit_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "Ghost" };
        PersistWithDatabase(db => db.Add(user));

        var command = new CreateHabitTrigger.CreateHabitTriggerCommand(
            9999,
            user.Id,
            "Invalid Habit",
            null,
            HabitTriggerType.Craving,
            null
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(ErrorResults.EntityNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task CreateHabitTrigger_WithTriggerHabitNotOwnedByUser_ShouldReturnNotFound()
    {
        var user1 = new User { DisplayName = "Owner" };
        var user2 = new User { DisplayName = "NotOwner" };
        var habit = new Habit { Title = "Main", User = user1 };
        var otherHabit = new Habit { Title = "Other", User = user2 };

        PersistWithDatabase(db => db.AddRange(habit, otherHabit));

        var command = new CreateHabitTrigger.CreateHabitTriggerCommand(
            habit.Id,
            user1.Id,
            "Bad Trigger",
            null,
            HabitTriggerType.Reward,
            otherHabit.Id
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(ErrorResults.EntityNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task CreateHabitTrigger_WithEmptyTitle_ShouldThrowValidationException()
    {
        var command = new CreateHabitTrigger.CreateHabitTriggerCommand(
            1,
            1,
            "",
            null,
            HabitTriggerType.Craving,
            null
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("Title", ex.Message);
    }

    [Fact]
    public async Task CreateHabitTrigger_WithTooLongTitle_ShouldThrowValidationException()
    {
        var command = new CreateHabitTrigger.CreateHabitTriggerCommand(
            1,
            1,
            new string('T', 65),
            null,
            HabitTriggerType.Craving,
            null
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("Title", ex.Message);
    }

    [Fact]
    public async Task CreateHabitTrigger_WithTooLongDescription_ShouldThrowValidationException()
    {
        var command = new CreateHabitTrigger.CreateHabitTriggerCommand(
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
    public async Task CreateHabitTrigger_WithInvalidEnum_ShouldThrowValidationException()
    {
        var command = new CreateHabitTrigger.CreateHabitTriggerCommand(
            1,
            1,
            "Invalid Enum",
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
