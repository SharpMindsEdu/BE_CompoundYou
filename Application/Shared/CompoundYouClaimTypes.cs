namespace Application.Shared;

/// <summary>
/// Custom JWT claim type identifiers used across CompoundYou. Kept stable
/// here so middleware, token issuance, and downstream readers all agree.
/// </summary>
public static class CompoundYouClaimTypes
{
    public const string TenantId = "tid";
    public const string MembershipId = "mid";
    public const string TenantRole = "role";
    public const string PlatformAdmin = "platform_admin";
}
