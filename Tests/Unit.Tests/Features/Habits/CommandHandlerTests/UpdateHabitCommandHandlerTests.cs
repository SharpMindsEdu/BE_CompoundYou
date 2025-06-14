using Application.Common;
using Application.Features.Habits.Commands;
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
}
