using Domain.Interfaces;

namespace Domain.Entities;

public class RoleProfile : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long JobFamilyId { get; set; }
    public long CareerLevelId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public JobFamily JobFamily { get; set; } = null!;
    public CareerLevel CareerLevel { get; set; } = null!;
    public ICollection<RoleProfileSkillRequirement> SkillRequirements { get; set; } =
        new List<RoleProfileSkillRequirement>();
    public ICollection<EmployeeRoleProfile> EmployeeAssignments { get; set; } =
        new List<EmployeeRoleProfile>();
}
