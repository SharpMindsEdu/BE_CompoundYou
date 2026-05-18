using Domain.Interfaces;

namespace Domain.Entities;

public class EmployeeRoleProfile : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long EmployeeId { get; set; }
    public long RoleProfileId { get; set; }
    public DateTimeOffset AssignedOn { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public RoleProfile RoleProfile { get; set; } = null!;
}
