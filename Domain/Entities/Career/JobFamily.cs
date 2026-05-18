using Domain.Interfaces;

namespace Domain.Entities;

public class JobFamily : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<CareerLevel> CareerLevels { get; set; } = new List<CareerLevel>();
    public ICollection<RoleProfile> RoleProfiles { get; set; } = new List<RoleProfile>();
}
