using Domain.Interfaces;

namespace Domain.Entities;

public class SkillCategory : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public ICollection<Skill> Skills { get; set; } = new List<Skill>();
}
