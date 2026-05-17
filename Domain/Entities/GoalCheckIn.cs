using Domain.Interfaces;

namespace Domain.Entities;

public class GoalCheckIn : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long GoalId { get; set; }
    public long AuthorEmployeeId { get; set; }
    public string? Note { get; set; }
    public decimal? ProgressValue { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Goal Goal { get; set; } = null!;
    public Employee AuthorEmployee { get; set; } = null!;
}
