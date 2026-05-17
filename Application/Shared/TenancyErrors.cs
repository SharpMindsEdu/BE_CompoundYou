namespace Application.Shared;

public static class TenancyErrors
{
    public const string TenantNotFound = "Tenant not found.";
    public const string SlugAlreadyTaken = "Tenant slug is already in use.";
    public const string TenantSuspended = "Tenant is suspended.";
    public const string MembershipNotFound = "Tenant membership not found.";
    public const string MembershipAlreadyExists = "User is already a member of this tenant.";
    public const string InvitationNotFound = "Invitation not found or invalid.";
    public const string InvitationExpired = "Invitation has expired.";
    public const string InvitationAlreadyAccepted = "Invitation was already accepted.";
    public const string CannotChangeOwnRole = "You cannot change your own role.";
    public const string CannotRemoveSelf = "You cannot remove yourself from the tenant.";
    public const string NoTenantInContext = "Request requires a tenant context.";
    public const string EmployeeNotFound = "Employee not found.";
    public const string EmployeeNumberInUse = "Employee number is already in use in this tenant.";
    public const string DepartmentNotFound = "Department not found.";
    public const string TeamNotFound = "Team not found.";
    public const string ManagerCycle = "Manager assignment would create a cycle.";
}
