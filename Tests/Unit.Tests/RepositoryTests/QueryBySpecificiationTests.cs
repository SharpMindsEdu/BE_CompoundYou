using Domain.Repositories;
using Infrastructure.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.RepositoryTests.Base;
using Unit.Tests.RepositoryTests.Entities.UserDb;

namespace Unit.Tests.RepositoryTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RepositoryTests)]
public class QueryBySpecificiationTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper,
    string? prefix = "T",
    Guid? dbId = null
) : RepositoryTestBase(fixture, outputHelper, prefix, dbId)
{
    class UserByFirstnameSpecification : BaseSpecification<User>
    {
        public UserByFirstnameSpecification(IRepository<User> repository, string firstname)
            : base(repository)
        {
            ApplyCriteria(x => x.Firstname == firstname);
        }
    }

    [Fact]
    public async Task QueryBySpecification_ReturnsFilteredResult_Success()
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

        var spec = new UserByFirstnameSpecification(userRepository, "Anna");
        var result = await userRepository.QueryBySpecification(
            spec,
            TestContext.Current.CancellationToken
        );

        Assert.Single(result);
        Assert.Equal("Anna", result[0].Firstname);
    }

    [Fact]
    public async Task QueryBySpecification_WithNoMatch_ReturnsEmptyResult()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();

        var spec = new UserByFirstnameSpecification(userRepository, "Unbekannt");
        var result = await userRepository.QueryBySpecification(
            spec,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(result);
    }

    [Fact]
    public async Task QueryBySpecification_WithEmptyDatabase_ReturnsEmptyResult()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();

        var spec = new UserByFirstnameSpecification(userRepository, "Anna");
        var result = await userRepository.QueryBySpecification(
            spec,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(result);
    }

    class UsersOrderedByFirstnameSpecification : BaseSpecification<User>
    {
        public UsersOrderedByFirstnameSpecification(IRepository<User> repository)
            : base(repository)
        {
            ApplyOrder(true, x => x.Firstname!);
        }
    }

    [Fact]
    public async Task QueryBySpecification_WithOrderByAscending_ReturnsOrderedResult()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Zoe",
            Lastname = "Müller",
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Anna",
            Lastname = "Smith",
        };
        await userRepository.Add(user1, user2);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var spec = new UsersOrderedByFirstnameSpecification(userRepository);
        var result = await userRepository.QueryBySpecification(
            spec,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.Count);
        Assert.Equal("Anna", result[0].Firstname);
        Assert.Equal("Zoe", result[1].Firstname);
    }

    class UsersOrderedByFirstnameDescendingSpecification : BaseSpecification<User>
    {
        public UsersOrderedByFirstnameDescendingSpecification(IRepository<User> repository)
            : base(repository)
        {
            ApplyOrder(false, x => x.Firstname!);
        }
    }

    [Fact]
    public async Task QueryBySpecification_WithOrderByDescending_ReturnsOrderedResult()
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
            Firstname = "Zoe",
            Lastname = "Müller",
        };
        await userRepository.Add(user1, user2);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var spec = new UsersOrderedByFirstnameDescendingSpecification(userRepository);
        var result = await userRepository.QueryBySpecification(
            spec,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.Count);
        Assert.Equal("Zoe", result[0].Firstname);
        Assert.Equal("Anna", result[1].Firstname);
    }

    class UserWithOrdersSpecification : BaseSpecification<User>
    {
        public UserWithOrdersSpecification(IRepository<User> repository)
            : base(repository)
        {
            AddInclude(x => x.Include(user => user.Orders));
        }
    }

    [Fact]
    public async Task QueryBySpecification_WithInclude_ReturnsRelatedEntities()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Anna",
            Lastname = "Smith",
            Orders = [new Order { Id = Guid.NewGuid(), Description = "Test Order" }],
        };

        await userRepository.Add(user);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var spec = new UserWithOrdersSpecification(userRepository);
        var result = await userRepository.QueryBySpecification(
            spec,
            TestContext.Current.CancellationToken
        );

        Assert.Single(result);
        Assert.Single(result[0].Orders);
        Assert.Equal("Test Order", result[0].Orders.First().Description);
    }

    [Fact]
    public async Task QueryBySpecification_WithSelector_ReturnsProjectedResult()
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

        var spec = new UserByFirstnameSpecification(userRepository, "Anna");
        var result = await userRepository.QueryBySpecification(
            spec,
            x => x.Firstname,
            TestContext.Current.CancellationToken
        );

        Assert.Single(result);
        Assert.Equal("Anna", result.First());
    }
}
