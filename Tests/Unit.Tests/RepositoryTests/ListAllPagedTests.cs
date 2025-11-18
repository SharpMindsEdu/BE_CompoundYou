using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.RepositoryTests.Base;
using Unit.Tests.RepositoryTests.Entities.UserDb;

namespace Unit.Tests.RepositoryTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RepositoryTests)]
public class ListAllPagedTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper,
    string? prefix = "T",
    Guid? dbId = null
) : RepositoryTestBase(fixture, outputHelper, prefix, dbId)
{
    [Fact]
    public async Task ListAllPaged_ReturnsPagedResult_Success()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        for (int i = 0; i < 30; i++)
        {
            await userRepository.Add(
                new User
                {
                    Id = Guid.NewGuid(),
                    Firstname = $"User{i}",
                    Lastname = "Test",
                }
            );
        }
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var page = await userRepository.ListAllPaged(
            page: 2,
            pageSize: 10,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(2, page.CurrentPage);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(30, page.TotalItems);
    }

    [Fact]
    public async Task ListAllPaged_PageSizeGreaterThanTotal_ReturnsAllResults()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        for (int i = 0; i < 5; i++)
        {
            await userRepository.Add(
                new User
                {
                    Id = Guid.NewGuid(),
                    Firstname = $"User{i}",
                    Lastname = "Test",
                }
            );
        }
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var page = await userRepository.ListAllPaged(
            page: 1,
            pageSize: 10,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(1, page.CurrentPage);
        Assert.Equal(5, page.Items.Count);
        Assert.Equal(5, page.TotalItems);
    }

    [Fact]
    public async Task ListAllPaged_EmptyDatabase_ReturnsEmptyPage()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();

        var page = await userRepository.ListAllPaged(
            page: 1,
            pageSize: 10,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(1, page.CurrentPage);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalItems);
    }

    [Fact]
    public async Task ListAllPaged_PageNumberExceedsTotalPages_ReturnsEmptyPage()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        for (int i = 0; i < 5; i++)
        {
            await userRepository.Add(
                new User
                {
                    Id = Guid.NewGuid(),
                    Firstname = $"User{i}",
                    Lastname = "Test",
                }
            );
        }
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var page = await userRepository.ListAllPaged(
            page: 3,
            pageSize: 4,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(3, page.CurrentPage);
        Assert.Empty(page.Items);
        Assert.Equal(5, page.TotalItems);
    }

    [Fact]
    public async Task ListAllPaged_PageSizeOne_ReturnsSingleResultPerPage()
    {
        var userRepository = ServiceProvider.GetRequiredService<IRepository<User>>();
        for (int i = 0; i < 3; i++)
        {
            await userRepository.Add(
                new User
                {
                    Id = Guid.NewGuid(),
                    Firstname = $"User{i}",
                    Lastname = "Test",
                }
            );
        }
        await userRepository.SaveChanges(TestContext.Current.CancellationToken);

        var page1 = await userRepository.ListAllPaged(
            page: 1,
            pageSize: 1,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var page2 = await userRepository.ListAllPaged(
            page: 2,
            pageSize: 1,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var page3 = await userRepository.ListAllPaged(
            page: 3,
            pageSize: 1,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Single(page1.Items);
        Assert.Single(page2.Items);
        Assert.Single(page3.Items);
        Assert.Equal(3, page1.TotalItems);
        Assert.Equal(3, page2.TotalItems);
        Assert.Equal(3, page3.TotalItems);
    }
}
