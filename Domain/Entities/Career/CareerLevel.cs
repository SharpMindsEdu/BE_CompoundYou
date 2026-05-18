using Domain.Interfaces;

namespace Domain.Entities;

public class CareerLevel : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long JobFamilyId { get; set; }
    public decimal Order { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public JobFamily JobFamily { get; set; } = null!;
    public ICollection<RoleProfile> RoleProfiles { get; set; } = new List<RoleProfile>();
}
