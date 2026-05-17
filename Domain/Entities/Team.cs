using Domain.Interfaces;

namespace Domain.Entities;

public class Team : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long DepartmentId { get; set; }
    public required string Name { get; set; }
    public long? ManagerEmployeeId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public Employee? ManagerEmployee { get; set; }
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
