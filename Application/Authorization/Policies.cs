namespace Application.Authorization;

/// <summary>
/// Named ASP.NET Core authorization policies used by Carter endpoints via
/// <c>RequireAuthorization(policy)</c>. Keep names stable; renaming requires
/// touching every endpoint that references them.
/// </summary>
public static class Policies
{
    public const string PlatformAdmin = nameof(PlatformAdmin);
    public const string TenantAdmin = nameof(TenantAdmin);
    public const string Manager = nameof(Manager);
    public const string Employee = nameof(Employee);
}
