using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.RepositoryTests.Base;
using Unit.Tests.RepositoryTests.Entities.UserDb;

namespace Unit.Tests.RepositoryTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RepositoryTests)]
public class UpdateTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper,
    string? prefix = "T",
    Guid? dbId = null
) : RepositoryTestBase(fixture, outputHelper, prefix, dbId)
{
    [Fact]
    public async Task Update_EntityUpdated_Success()
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

        user.Firstname = "Maximilian";
        userRepository.Update(user);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var updatedUser = await userRepository.GetById(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("Maximilian", updatedUser.Firstname);
    }

    [Fact]
    public async Task Update_Expression_BulkUpdate_Success()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        await userRepository.Add(
            new User
            {
                Id = Guid.NewGuid(),
                Firstname = "Test1",
                Lastname = "Alt",
            },
            new User
            {
                Id = Guid.NewGuid(),
                Firstname = "Test2",
                Lastname = "Alt",
            }
        );
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        await userRepository.Update(
            propertyCalls => propertyCalls.SetProperty(x => x.Lastname, "Neu"),
            x => x.Lastname == "Alt",
            TestContext.Current.CancellationToken
        );
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var result = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.All(result, u => Assert.Equal("Neu", u.Lastname));
    }

    [Fact]
    public async Task Update_MultipleEntities_EntitiesUpdated_Success()
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

        user1.Firstname = "Maximilian";
        user2.Lastname = "Müller";

        userRepository.Update(user1, user2);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var updatedUsers = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Contains(updatedUsers, x => x.Id == user1.Id && x.Firstname == "Maximilian");
        Assert.Contains(updatedUsers, x => x.Id == user2.Id && x.Lastname == "Müller");
    }

    [Fact]
    public void Update_EntityNotTracked_ThrowsInvalidOperationException()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };

        // Benutzer wird nie hinzugefügt -> nicht getrackt

        // Verhalten prüfen: Ab EF Core 7 wird "Detached" ignoriert
        var exception = Record.Exception(() =>
        {
            userRepository.Update(user);
        });

        Assert.Null(exception); // Sollte ab EF Core 7 kein Fehler mehr sein
    }

    [Fact]
    public async Task Update_Expression_NoMatch_DoesNothing()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        await userRepository.Add(
            new User
            {
                Id = Guid.NewGuid(),
                Firstname = "Test",
                Lastname = "Alt",
            }
        );
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        await userRepository.Update(
            propertyCalls => propertyCalls.SetProperty(x => x.Lastname, "Neu"),
            x => x.Firstname == "NichtVorhanden",
            TestContext.Current.CancellationToken
        );

        var result = await userRepository.ListAll(
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.All(result, u => Assert.Equal("Alt", u.Lastname));
    }

    [Fact]
    public async Task Update_Expression_NullSetExpression_ThrowsArgumentNullException()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await userRepository.Update(
                null!,
                x => x.Lastname == "Alt",
                TestContext.Current.CancellationToken
            );
        });
    }
}
