using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.RepositoryTests.Base;
using Unit.Tests.RepositoryTests.Entities.UserDb;

namespace Unit.Tests.RepositoryTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RepositoryTests)]
public class AddTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper,
    string? prefix = "T",
    Guid? dbId = null
) : RepositoryTestBase(fixture, outputHelper, prefix, dbId)
{
    [Fact]
    public async Task AddUsers_ReturnsAddedUserOnGetById_Success()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var expectedUser = new User()
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };
        await userRepository.Add(expectedUser);

        await userRepository.SaveChanges(TestContext.Current.CancellationToken);
        var result = await userRepository.GetById(expectedUser.Id);
        Assert.NotNull(result);
        Assert.Equal(expectedUser.Firstname, result.Firstname);
        Assert.Equal(expectedUser.Lastname, result.Lastname);
    }

    [Fact]
    public async Task AddUsers_ReturnsAddedUsersOnListAll_Success()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var expectedUser1 = new User()
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };
        var expectedUser2 = new User()
        {
            Id = Guid.NewGuid(),
            Firstname = "Luisa",
            Lastname = "Luftgitarre",
        };
        await userRepository.Add(expectedUser1, expectedUser2);

        await userRepository.SaveChanges(TestContext.Current.CancellationToken);
        var result = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(
            result,
            x =>
                x.Id == expectedUser1.Id
                && x.Firstname == expectedUser1.Firstname
                && x.Lastname == expectedUser1.Lastname
        );
        Assert.Contains(
            result,
            x =>
                x.Id == expectedUser2.Id
                && x.Firstname == expectedUser2.Firstname
                && x.Lastname == expectedUser2.Lastname
        );
    }

    [Fact]
    public async Task AddUser_WithNullValues_ThrowsArgumentNullException()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        await Assert.ThrowsAsync<NullReferenceException>(() => userRepository.Add(null!));
    }

    [Fact]
    public async Task AddUser_WithDuplicateId_ThrowsDbUpdateException()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };
        var user2 = new User
        {
            Id = user1.Id,
            Firstname = "Luisa",
            Lastname = "Luftgitarre",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => userRepository.Add(user1, user2));
    }

    [Fact]
    public async Task AddUser_WithDuplicateId_OneAfterTHeOthers_ThrowsDbUpdateException()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };
        var user2 = new User
        {
            Id = user1.Id,
            Firstname = "Luisa",
            Lastname = "Luftgitarre",
        };

        await userRepository.Add(user1);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);
        await userRepository.Add(user2);
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            userRepository.SaveChanges(TestContext.Current.CancellationToken)
        );
    }

    private async Task<User> AddUser()
    {
        using var scope = ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };

        await userRepository.Add(user1);
        await userRepository.SaveChanges();
        return user1;
    }

    [Fact]
    public async Task AddUser_WithDuplicateIdOnAlreadyExistingEntry_ThrowsDbUpdateException()
    {
        var user2 = new User
        {
            Id = (await AddUser()).Id,
            Firstname = "Luisa",
            Lastname = "Luftgitarre",
        };
        using var scope = ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
        await userRepository.Add(user2);
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            userRepository.SaveChanges(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task AddMultipleUsers_WithEmptyList_DoesNothing()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        await userRepository.Add();
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var result = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Empty(result);
    }
}
