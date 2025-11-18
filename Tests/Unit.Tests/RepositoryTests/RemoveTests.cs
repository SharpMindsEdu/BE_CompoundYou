using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.RepositoryTests.Base;
using Unit.Tests.RepositoryTests.Entities.UserDb;

namespace Unit.Tests.RepositoryTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RepositoryTests)]
public class RemoveTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper,
    string? prefix = "T",
    Guid? dbId = null
) : RepositoryTestBase(fixture, outputHelper, prefix, dbId)
{
    [Fact]
    public async Task Remove_EntityRemoved_Success()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };
        await userRepository.Add(user);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        userRepository.Remove(user);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var result = await userRepository.GetById(user.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task Remove_Expression_EntityRemoved_Success()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };
        await userRepository.Add(user);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        await userRepository.Remove(x => x.Id == user.Id, TestContext.Current.CancellationToken);

        var result = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Empty(result);
    }

    [Fact]
    public void Remove_Entity_NotTracked_ThrowsInvalidOperationException()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };

        // Ohne Add und SaveChanges: User ist nicht getrackt
        var exception = Record.Exception(() =>
        {
            userRepository.Remove(user);
        });

        Assert.Null(exception); // In EF Core 7 ist das kein Fehler mehr, wird als "Detached" ignoriert.
    }

    [Fact]
    public async Task Remove_MultipleEntities_RemovedSuccessfully()
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
            Id = Guid.NewGuid(),
            Firstname = "Anna",
            Lastname = "Schmidt",
        };

        await userRepository.Add(user1, user2);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        userRepository.Remove(user1, user2);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var result = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Empty(result);
    }

    [Fact]
    public async Task Remove_Expression_NoMatch_DoesNothing()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };
        await userRepository.Add(user1);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        await userRepository.Remove(
            x => x.Firstname == "NichtVorhanden",
            TestContext.Current.CancellationToken
        );
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var result = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Single(result);
    }

    [Fact]
    public async Task Remove_EmptyExpression_DoesNothing()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };
        await userRepository.Add(user1);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        await userRepository.Remove(u => false, TestContext.Current.CancellationToken);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var result = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Single(result);
    }

    [Fact]
    public void Remove_Entity_Null_DoesNothing()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();

        var exception = Record.Exception(() =>
        {
            userRepository.Remove(null!);
        });

        Assert.NotNull(exception);
        Assert.IsType<ArgumentNullException>(exception);
    }

    [Fact]
    public void Remove_Expression_NullPredicate_ThrowsArgumentNullException()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();

        Assert.Throws<ArgumentNullException>(() =>
        {
            userRepository.Remove(null!);
        });
    }
}
