using Application.Common;
using Application.Features.Habits.Commands;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class DeleteHabitCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task DeleteHabit_WithValidHabitAndUser_ShouldSucceed()
    {
        var user = new User { DisplayName = "Deleter" };
        var habit = new Habit
        {
            Title = "Delete Me",
            Score = 10,
            User = user,
        };

        PersistWithDatabase(db => db.Add(habit));

        var command = new DeleteHabit.DeleteHabitCommand(habit.Id, user.Id);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task DeleteHabit_WithHabitNotBelongingToUser_ShouldReturnNotFound()
    {
        var user1 = new User { DisplayName = "Owner" };
        var user2 = new User { DisplayName = "Intruder" };

        var habit = new Habit
        {
            Title = "Protected Habit",
            Score = 10,
            User = user1,
        };

        PersistWithDatabase(db => db.AddRange(user1, user2, habit));

        var command = new DeleteHabit.DeleteHabitCommand(habit.Id, user2.Id);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(ErrorResults.EntityNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteHabit_WithNonexistentHabit_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "Ghost" };
        PersistWithDatabase(db => db.Add(user));

        var command = new DeleteHabit.DeleteHabitCommand(9999, user.Id); // Nicht vorhandene HabitId

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(ErrorResults.EntityNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteHabit_WithInvalidHabitId_ShouldThrowValidationException()
    {
        var command = new DeleteHabit.DeleteHabitCommand(0, 1); // 0 ist ungültig lt. Rule

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("HabitId", ex.Message);
    }

    [Fact]
    public async Task DeleteHabit_WithNullUserId_ShouldThrowValidationException()
    {
        var command = new DeleteHabit.DeleteHabitCommand(1, null); // null ist ungültig

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public async Task DeleteHabit_WithNegativeUserId_ShouldThrowValidationException()
    {
        var command = new DeleteHabit.DeleteHabitCommand(1, -5); // -1 ist lt. Rule ungültig

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }
}
