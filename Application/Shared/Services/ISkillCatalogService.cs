using Domain.Entities;

namespace Application.Shared.Services;

/// <summary>
/// Provides access to the global skill catalog with caching.
/// Used to avoid frequent database lookups for standard skills.
/// </summary>
public interface ISkillCatalogService
{
    Task<IReadOnlyList<Skill>> GetGlobalSkillsAsync(CancellationToken ct = default);
    Task<Skill?> GetGlobalSkillAsync(long skillId, CancellationToken ct = default);
}
