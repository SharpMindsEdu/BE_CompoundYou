using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.RepositoryTests.Base;

namespace Unit.Tests.RepositoryTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RepositoryTests)]
public class RepositoryContextResolutionTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper,
    string? prefix = "T",
    Guid? dbId = null
) : RepositoryTestBase(fixture, outputHelper, prefix, dbId)
{
    [Fact]
    public async Task Repository_UserEntity_UsesUserTestDbContext()
    {
        var userRepository = ServiceProvider.GetRequiredService<
            IRepository<Entities.UserDb.User>
        >();

        var user = new Entities.UserDb.User
        {
            Id = Guid.NewGuid(),
            Firstname = "Max",
            Lastname = "Mustermann",
        };
        await userRepository.Add(user);
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var resolvedUser = await userRepository.GetById(user.Id);
        Assert.NotNull(resolvedUser);
        Assert.Equal("Max", resolvedUser.Firstname);
    }

    [Fact]
    public async Task Repository_UserEntity_UsesEmployeeTestDbContext()
    {
        var employeeUserRepository = ServiceProvider.GetRequiredService<
            IRepository<Entities.EmployeeDb.User>
        >();

        var employeeUser = new Entities.EmployeeDb.User
        {
            Id = Guid.NewGuid(),
            Firstname = "Anna",
            Lastname = "Müller",
        };
        await employeeUserRepository.Add(employeeUser);
        await employeeUserRepository.SaveChanges(TestContext.Current.CancellationToken);

        var resolvedEmployeeUser = await employeeUserRepository.GetById(employeeUser.Id);
        Assert.NotNull(resolvedEmployeeUser);
        Assert.Equal("Anna", resolvedEmployeeUser.Firstname);
    }
}
