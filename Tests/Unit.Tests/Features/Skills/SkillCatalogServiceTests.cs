using Application.Shared.Services;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace Unit.Tests.Features.Skills;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.SkillTests)]
public class SkillCatalogServiceTests
{
    private readonly Mock<IRepository<Skill>> _skillRepositoryMock;
    private readonly IMemoryCache _cache;
    private readonly SkillCatalogService _service;

    public SkillCatalogServiceTests()
    {
        _skillRepositoryMock = new Mock<IRepository<Skill>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new SkillCatalogService(_skillRepositoryMock.Object, _cache);
    }

    [Fact]
    public async Task GetGlobalSkillsAsync_ShouldCacheResults()
    {
        // Arrange
        var globalSkills = new List<Skill>
        {
            new() { Id = 1, Name = "Skill 1", TenantId = null, IsActive = true },
            new() { Id = 2, Name = "Skill 2", TenantId = null, IsActive = true }
        };

        _skillRepositoryMock
            .Setup(r => r.QueryBySpecification(It.IsAny<ISpecification<Skill>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(globalSkills);

        // Act
        var result1 = await _service.GetGlobalSkillsAsync();
        var result2 = await _service.GetGlobalSkillsAsync();

        // Assert
        Assert.Equal(globalSkills.Count, result1.Count);
        Assert.Equal(globalSkills.Count, result2.Count);
        
        // Verify repository was called only once
        _skillRepositoryMock.Verify(
            r => r.QueryBySpecification(It.IsAny<ISpecification<Skill>>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetGlobalSkillAsync_ShouldReturnFromCache()
    {
        // Arrange
        var globalSkills = new List<Skill>
        {
            new() { Id = 1, Name = "Skill 1", TenantId = null, IsActive = true }
        };

        _skillRepositoryMock
            .Setup(r => r.QueryBySpecification(It.IsAny<ISpecification<Skill>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(globalSkills);

        // Act
        var result = await _service.GetGlobalSkillAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Skill 1", result.Name);
    }
}
