using Domain.Enums;

namespace Domain.Entities;

public class TenantMembership : TrackedEntity
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public TenantRole Role { get; set; }
    public DateTimeOffset JoinedOn { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
}
