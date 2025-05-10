using Application.Features.Habits.Commands;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Habits.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.HabitTests)]
public class CreateHabitCommandHandlerTests(PostgreSqlRepositoryTestDatabaseFixture fixture, ITestOutputHelper outputHelper) 
    : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateHabit_WithValidData_ShouldCreateHabit()
    {
        var user = new User()
        {
            DisplayName = "Test User"
        };
        PersistWithDatabase(db => db.Add(user));
        var habit = new CreateHabit.CreateHabitCommand(user.Id, "Test Habit", 98, "Test Description", "Test Motivation");

        var result = await Send(habit, TestContext.Current.CancellationToken);
        
        Assert.NotNull(result.Data);
        Assert.Equal(habit.Title, result.Data.Title);
        Assert.Equal(habit.Description, result.Data.Description);
        Assert.Equal(habit.Motivation, result.Data.Motivation);
        Assert.Equal(habit.Score, result.Data.Score);
    }
    
}