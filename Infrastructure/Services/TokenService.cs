using Application.Features.Users.Services;
using Application.Shared;
using Domain.Entities;

namespace Infrastructure.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public class TokenService(IConfiguration config) : ITokenService
{
    public string CreateToken(User user, TenantContextClaims? tenantContext = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
        };

        if (user.IsPlatformAdmin)
        {
            claims.Add(new Claim(CompoundYouClaimTypes.PlatformAdmin, "true"));
        }

        if (tenantContext is not null)
        {
            claims.Add(new Claim(CompoundYouClaimTypes.TenantId, tenantContext.TenantId.ToString()));
            claims.Add(new Claim(CompoundYouClaimTypes.MembershipId, tenantContext.MembershipId.ToString()));
            claims.Add(new Claim(CompoundYouClaimTypes.TenantRole, tenantContext.Role.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
