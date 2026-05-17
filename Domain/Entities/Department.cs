using Domain.Interfaces;

namespace Domain.Entities;

public class Department : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public required string Name { get; set; }
    public long? ParentDepartmentId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Department? ParentDepartment { get; set; }
    public ICollection<Department> ChildDepartments { get; set; } = new List<Department>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
