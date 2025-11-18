using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.RepositoryTests.Base;
using Unit.Tests.RepositoryTests.Entities.UserDb;

namespace Unit.Tests.RepositoryTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RepositoryTests)]
public class ListAllTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper,
    string? prefix = "T",
    Guid? dbId = null
) : RepositoryTestBase(fixture, outputHelper, prefix, dbId)
{
    [Fact]
    public async Task ListAll_WithSelector_ReturnsProjectedResult_Success()
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

        var result = await userRepository.ListAll(
            x => x.Firstname,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Single(result);
        Assert.Contains("Anna", result);
    }

    [Fact]
    public async Task ListAll_WithoutPredicate_ReturnsAllEntities()
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

        var result = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Firstname == "Anna");
        Assert.Contains(result, x => x.Firstname == "Max");
    }

    [Fact]
    public async Task ListAll_WithPredicate_ReturnsFilteredEntities()
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

        var result = await userRepository.ListAll(
            x => x.Firstname == "Anna",
            TestContext.Current.CancellationToken
        );

        Assert.Single(result);
        Assert.Equal("Anna", result.First().Firstname);
    }

    [Fact]
    public async Task ListAll_WithSelectorAndPredicate_ReturnsProjectedFilteredResults()
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

        var result = await userRepository.ListAll(
            x => x.Firstname,
            x => x.Firstname == "Anna",
            TestContext.Current.CancellationToken
        );

        Assert.Single(result);
        Assert.Equal("Anna", result.First());
    }

    [Fact]
    public async Task ListAll_EmptyDatabase_ReturnsEmptyList()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();

        var result = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAll_WithSelector_EmptyDatabase_ReturnsEmptyList()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();

        var result = await userRepository.ListAll(
            x => x.Firstname,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Empty(result);
    }
}
