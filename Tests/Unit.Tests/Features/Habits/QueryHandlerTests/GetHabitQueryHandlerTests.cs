using Application.Features.Habits.Queries;
using Application.Shared;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.QueryHandlerTests;

public class GetHabitQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetHabit_WithValidIdAndUser_ShouldReturnHabit()
    {
        var user = new User { DisplayName = "QueryUser" };
        var habit = new Habit
        {
            Title = "Read Book",
            Score = 50,
            Description = "Read a chapter a day",
            Motivation = "Self improvement",
            User = user,
        };

        PersistWithDatabase(db => db.Add(habit));

        var query = new GetHabit.GetHabitQuery(habit.Id, user.Id);

        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(habit.Title, result.Data.Title);
        Assert.Equal(habit.Description, result.Data.Description);
        Assert.Equal(habit.Motivation, result.Data.Motivation);
        Assert.Equal(habit.Score, result.Data.Score);
    }

    [Fact]
    public async Task GetHabit_WithInvalidHabitId_ShouldReturnNotFound()
    {
        var user = new User { DisplayName = "MissingHabitUser" };
        PersistWithDatabase(db => db.Add(user));

        var query = new GetHabit.GetHabitQuery(99999, user.Id); // Nicht existierende HabitId

        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetHabit_WithWrongUser_ShouldReturnNotFound()
    {
        var user1 = new User { DisplayName = "Owner" };
        var user2 = new User { DisplayName = "OtherUser" };

        var habit = new Habit
        {
            Title = "Private Habit",
            Score = 20,
            Description = "Only for user1",
            Motivation = "Privacy",
            User = user1,
        };

        PersistWithDatabase(db => db.AddRange(user2, habit));

        var query = new GetHabit.GetHabitQuery(habit.Id, user2.Id); // Falscher Benutzer

        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetHabit_WithZeroUserId_ShouldThrowValidationException()
    {
        var query = new GetHabit.GetHabitQuery(1, 0);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(query, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public async Task GetHabit_WithNullUserId_ShouldThrowValidationException()
    {
        var query = new GetHabit.GetHabitQuery(1, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(query, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public async Task GetHabit_WithZeroHabitId_ShouldThrowValidationException()
    {
        var query = new GetHabit.GetHabitQuery(0, 1);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(query, TestContext.Current.CancellationToken)
        );
        Assert.Contains("HabitId", ex.Message);
    }
}
