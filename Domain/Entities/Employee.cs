using Domain.Interfaces;

namespace Domain.Entities;

public class Employee : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long UserId { get; set; }
    public required string EmployeeNumber { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateOnly? HireDate { get; set; }
    public long? TeamId { get; set; }
    public long? ManagerEmployeeId { get; set; }
    public string? ExternalSourceId { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
    public Team? Team { get; set; }
    public Employee? ManagerEmployee { get; set; }
    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();
}
