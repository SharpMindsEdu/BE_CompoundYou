using Application.Features.Habits.Commands;
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
