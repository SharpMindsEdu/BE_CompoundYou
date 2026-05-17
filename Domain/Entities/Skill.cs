using Domain.Interfaces;

namespace Domain.Entities;

public class Skill : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long SkillCategoryId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public long? ParentSkillId { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public SkillCategory SkillCategory { get; set; } = null!;
    public Skill? ParentSkill { get; set; }
    public ICollection<Skill> ChildSkills { get; set; } = new List<Skill>();
    public ICollection<SkillLevel> SkillLevels { get; set; } = new List<SkillLevel>();
}
