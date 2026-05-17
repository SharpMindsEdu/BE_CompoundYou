using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Users.Services;

public interface ITokenService
{
    string CreateToken(User user, TenantContextClaims? tenantContext = null);
}

public sealed record TenantContextClaims(long TenantId, long MembershipId, TenantRole Role);
