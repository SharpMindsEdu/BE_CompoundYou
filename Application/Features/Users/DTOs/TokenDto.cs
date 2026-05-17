using Application.Features.Tenants.DTOs;

namespace Application.Features.Users.DTOs;

/// <summary>
/// Response payload for token-issuing endpoints (Register, Login, SwitchTenant).
/// When the caller has multiple active tenant memberships, <see cref="Token"/>
/// is issued without a tenant claim and <see cref="RequiresTenantSelection"/>
/// is true; the client must call <c>SwitchTenant</c> with one of
/// <see cref="AvailableTenants"/> to obtain a tenant-bound token.
/// </summary>
public record TokenDto(
    string Token,
    bool RequiresTenantSelection = false,
    IReadOnlyList<TenantOptionDto>? AvailableTenants = null
);
