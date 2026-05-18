using Application.Features.Skills.Specifications;
using Application.Shared;
using Application.Shared.Services;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public class SkillCatalogService(IRepository<Skill> skills, IMemoryCache cache) : ISkillCatalogService
{
    private const string GlobalSkillsCacheKey = "GlobalSkillsCatalog";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<Skill>> GetGlobalSkillsAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(GlobalSkillsCacheKey, out IReadOnlyList<Skill>? cachedSkills) && cachedSkills != null)
        {
            return cachedSkills;
        }

        var spec = new GlobalSkillsSpec();
        var globalSkills = await skills.QueryBySpecification(spec, ct);

        cache.Set(GlobalSkillsCacheKey, globalSkills, CacheDuration);

        return globalSkills;
    }

    public async Task<Skill?> GetGlobalSkillAsync(long skillId, CancellationToken ct = default)
    {
        var all = await GetGlobalSkillsAsync(ct);
        return all.FirstOrDefault(s => s.Id == skillId);
    }

    private sealed class GlobalSkillsSpec : BaseSpecification<Skill>
    {
        public GlobalSkillsSpec()
        {
            ApplyCriteria(s => s.TenantId == null && s.IsActive);
            AddInclude(q => q.Include(s => s.SkillCategory));
        }
    }
}
