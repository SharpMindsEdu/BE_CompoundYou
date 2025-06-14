using Application.Features.Habits.Commands;
using Application.Features.Habits.DTOs;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class CreateHabitCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateHabit_WithValidData_ShouldCreateHabit()
    {
        var user = new User { DisplayName = "Test User" };
        PersistWithDatabase(db => db.Add(user));

        var command = new CreateHabit.CreateHabitCommand(
            user.Id,
            "Test Habit",
            98,
            "Test Description",
            "Test Motivation"
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(command.Title, result.Data.Title);
        Assert.Equal(command.Description, result.Data.Description);
        Assert.Equal(command.Motivation, result.Data.Motivation);
        Assert.Equal(command.Score, result.Data.Score);
    }

    [Fact]
    public async Task CreateHabit_WithValidTimes_ShouldCreateHabitWithTimes()
    {
        var user = new User { DisplayName = "Scheduler" };
        PersistWithDatabase(db => db.Add(user));

        var times = new List<HabitTimeDto>
        {
            new(0, DayOfWeek.Monday, new TimeSpan(7, 30, 0)),
            new(0, DayOfWeek.Friday, new TimeSpan(18, 0, 0)),
        };

        var command = new CreateHabit.CreateHabitCommand(
            user.Id,
            "Habit with Times",
            10,
            null,
            null,
            times
        );

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data.Times);
        Assert.Equal(2, result.Data.Times.Count);
        Assert.Contains(
            result.Data.Times,
            t => t.Day == DayOfWeek.Monday && t.Time == new TimeSpan(7, 30, 0)
        );
        Assert.Contains(
            result.Data.Times,
            t => t.Day == DayOfWeek.Friday && t.Time == new TimeSpan(18, 0, 0)
        );
    }

    [Fact]
    public async Task CreateHabit_WithZeroTime_ShouldThrowValidationException()
    {
        var user = new User { DisplayName = "ZeroTimeTester" };
        PersistWithDatabase(db => db.Add(user));

        var times = new List<HabitTimeDto> { new(0, DayOfWeek.Monday, TimeSpan.Zero) };

        var command = new CreateHabit.CreateHabitCommand(
            user.Id,
            "Invalid Time Habit",
            20,
            null,
            null,
            times
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("Time", ex.Message);
    }

    [Fact]
    public async Task CreateHabit_WithNullTimes_ShouldSucceed()
    {
        var user = new User { DisplayName = "NullTimeUser" };
        PersistWithDatabase(db => db.Add(user));

        var command = new CreateHabit.CreateHabitCommand(user.Id, "No Times Habit", 30, null, null);

        var result = await Send(command, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Times);
    }

    [Fact]
    public async Task CreateHabit_WithNullUserId_ShouldThrowValidationException()
    {
        var command = new CreateHabit.CreateHabitCommand(null, "Test Habit", 98, null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public async Task CreateHabit_WithEmptyTitle_ShouldThrowValidationException()
    {
        var command = new CreateHabit.CreateHabitCommand(1, "", 98, null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Title", ex.Message);
    }

    [Fact]
    public async Task CreateHabit_WithTooLongTitle_ShouldThrowValidationException()
    {
        var command = new CreateHabit.CreateHabitCommand(1, new string('A', 25), 98, null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Title", ex.Message);
    }

    [Fact]
    public async Task CreateHabit_WithTooLongDescription_ShouldThrowValidationException()
    {
        var command = new CreateHabit.CreateHabitCommand(
            1,
            "Test",
            98,
            new string('D', 1501),
            null
        );

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Description", ex.Message);
    }

    [Fact]
    public async Task CreateHabit_WithTooLongMotivation_ShouldThrowValidationException()
    {
        var command = new CreateHabit.CreateHabitCommand(1, "Test", 98, null, new string('M', 421));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("Motivation", ex.Message);
    }

    [Fact]
    public async Task CreateHabit_WithNegativeUserId_ShouldThrowValidationException()
    {
        var command = new CreateHabit.CreateHabitCommand(-1, "Test", 98, null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(command, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }
}
