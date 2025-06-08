using Application.Common;
using Application.Features.Habits.Queries;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class GetHabitsQueryHandlerTests(PostgreSqlRepositoryTestDatabaseFixture fixture, ITestOutputHelper outputHelper)
    : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetHabits_WithMultipleHabits_ShouldReturnAllHabits()
    {
        var user = new User { DisplayName = "MultiHabitUser" };
        var habit1 = new Habit
        {
            Title = "Morning Run",
            Score = 30,
            Description = "Jog for 20 minutes",
            Motivation = "Fitness",
            User = user
        };
        var habit2 = new Habit
        {
            Title = "Read Book",
            Score = 20,
            Description = "Read 10 pages",
            Motivation = "Knowledge",
            User = user
        };

        PersistWithDatabase(db => db.AddRange(habit1, habit2));

        var query = new GetHabits.GetHabitsQuery(user.Id);

        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, h => h.Title == habit1.Title);
        Assert.Contains(result.Data, h => h.Title == habit2.Title);
    }

    [Fact]
    public async Task GetHabits_WithNoHabits_ShouldReturnEmptyList()
    {
        var user = new User { DisplayName = "NoHabitUser" };
        PersistWithDatabase(db => db.Add(user));

        var query = new GetHabits.GetHabitsQuery(user.Id);

        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetHabits_WithNullUserId_ShouldThrowValidationException()
    {
        var query = new GetHabits.GetHabitsQuery(null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Send(query, TestContext.Current.CancellationToken));
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public async Task GetHabits_WithZeroUserId_ShouldThrowValidationException()
    {
        var query = new GetHabits.GetHabitsQuery(0);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Send(query, TestContext.Current.CancellationToken));
        Assert.Contains("UserId", ex.Message);
    }
}
