using Application.Shared;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Skills.Specifications;

/// <summary>
/// Specification for skills visible to a tenant.
/// Includes SkillLevels and SkillCategory by default.
/// Note: The tenant filter (TenantId == null || TenantId == CurrentTenantId) 
/// is automatically applied via the global query filter in ApplicationDbContext.
/// </summary>
public sealed class SkillsVisibleToTenantSpec : BaseSpecification<Skill>
{
    public SkillsVisibleToTenantSpec()
    {
        ApplyCriteria(s => s.IsActive);
        AddInclude(q => q.Include(s => s.SkillLevels));
        AddInclude(q => q.Include(s => s.SkillCategory));
        ApplyOrder(true, s => s.Name);
    }
}
