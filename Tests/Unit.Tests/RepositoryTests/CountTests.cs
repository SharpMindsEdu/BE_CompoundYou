using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.RepositoryTests.Base;
using Unit.Tests.RepositoryTests.Entities.UserDb;

namespace Unit.Tests.RepositoryTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RepositoryTests)]
public class CountTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper,
    string? prefix = "CountTests",
    Guid? dbId = null
) : RepositoryTestBase(fixture, outputHelper, prefix, dbId)
{
    [Fact]
    public async Task Count_ReturnsCorrectCount_Success()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Anna",
            Lastname = "Smith",
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Müller",
        };
        await userRepository.Add(user1, user2);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var count = await userRepository.Count(
            x => x.Firstname!.Contains("a"),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Count_WithEmptyDatabase_ReturnsZero()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();

        var count = await userRepository.Count(
            _ => true,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Count_WithPredicateThatMatchesNothing_ReturnsZero()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Anna",
            Lastname = "Smith",
        };
        await userRepository.Add(user1);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var count = await userRepository.Count(
            x => x.Firstname == "Unbekannt",
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Count_WithoutPredicate_ReturnsTotalCount()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Anna",
            Lastname = "Smith",
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Müller",
        };
        await userRepository.Add(user1, user2);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var count = await userRepository.Count(
            null,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Count_WithSelector_ReturnsCountBasedOnProjection()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Anna",
            Lastname = "Smith",
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Müller",
        };
        await userRepository.Add(user1, user2);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        // Hier wird die Projektion auf den Nachnamen gemacht, sollte die gleiche Anzahl liefern
        var count = await userRepository.Count(
            _ => true,
            x => x.Lastname!,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(2, count);
    }
}
