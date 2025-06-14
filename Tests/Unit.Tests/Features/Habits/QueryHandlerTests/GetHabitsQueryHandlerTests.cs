using Application.Features.Habits.Queries;
using Domain.Entities;
using FluentValidation;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class GetHabitsQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
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
            User = user,
        };
        var habit2 = new Habit
        {
            Title = "Read Book",
            Score = 20,
            Description = "Read 10 pages",
            Motivation = "Knowledge",
            User = user,
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
    public async Task GetHabits_WithScoreFilter_ShouldReturnMatchingHabitsOnly()
    {
        var user = new User { DisplayName = "FilterUser" };
        var habit1 = new Habit
        {
            Title = "Sleep Early",
            Score = 80,
            User = user,
        };
        var habit2 = new Habit
        {
            Title = "Eat Candy",
            Score = 10,
            User = user,
        };

        PersistWithDatabase(db => db.AddRange(habit1, habit2));

        var query = new GetHabits.GetHabitsQuery(UserId: user.Id, MinScore: 50);

        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("Sleep Early", result.Data.First().Title);
    }

    [Fact]
    public async Task GetHabits_WithIsPreparationHabitTrue_ShouldReturnOnlyPreparationHabits()
    {
        var user = new User { DisplayName = "PreparationUser" };
        var habit1 = new Habit
        {
            Title = "Plan Day",
            Score = 70,
            IsPreparationHabit = true,
            User = user,
        };
        var habit2 = new Habit
        {
            Title = "Exercise",
            Score = 60,
            IsPreparationHabit = false,
            User = user,
        };

        PersistWithDatabase(db => db.AddRange(habit1, habit2));

        var query = new GetHabits.GetHabitsQuery(user.Id, IsPreparationHabit: true);

        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("Plan Day", result.Data.First().Title);
    }

    [Fact]
    public async Task GetHabits_WithScoreRange_ShouldReturnHabitsWithinRange()
    {
        var user = new User { DisplayName = "RangeUser" };
        var habit1 = new Habit
        {
            Title = "Meditation",
            Score = 90,
            User = user,
        };
        var habit2 = new Habit
        {
            Title = "Social Media",
            Score = 40,
            User = user,
        };
        var habit3 = new Habit
        {
            Title = "Healthy Eating",
            Score = 70,
            User = user,
        };

        PersistWithDatabase(db => db.AddRange(habit1, habit2, habit3));

        var query = new GetHabits.GetHabitsQuery(user.Id, MinScore: 60, MaxScore: 95);

        var result = await Send(query, TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, h => h.Title == "Meditation");
        Assert.Contains(result.Data, h => h.Title == "Healthy Eating");
    }

    [Fact]
    public async Task GetHabits_WithTitleFilter_ShouldReturnMatchingHabits()
    {
        var user = new User { DisplayName = "TitleUser" };
        var habit1 = new Habit
        {
            Title = "Read Articles",
            Score = 60,
            User = user,
        };
        var habit2 = new Habit
        {
            Title = "Reading Notes",
            Score = 70,
            User = user,
        };
        var habit3 = new Habit
        {
            Title = "Exercise",
            Score = 50,
            User = user,
        };

        PersistWithDatabase(db => db.AddRange(habit1, habit2, habit3));

        var query = new GetHabits.GetHabitsQuery(user.Id, Title: "read");

        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.All(
            result.Data,
            h => Assert.Contains("read", h.Title, StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public async Task GetHabits_WithUnmatchedTitle_ShouldReturnEmptyList()
    {
        var user = new User { DisplayName = "NoMatchUser" };
        var habit = new Habit
        {
            Title = "Workout",
            Score = 80,
            User = user,
        };

        PersistWithDatabase(db => db.Add(habit));

        var query = new GetHabits.GetHabitsQuery(user.Id, Title: "xyz");

        var result = await Send(query, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetHabits_WithNullUserId_ShouldThrowValidationException()
    {
        var query = new GetHabits.GetHabitsQuery(null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(query, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public async Task GetHabits_WithZeroUserId_ShouldThrowValidationException()
    {
        var query = new GetHabits.GetHabitsQuery(0);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Send(query, TestContext.Current.CancellationToken)
        );
        Assert.Contains("UserId", ex.Message);
    }
}
