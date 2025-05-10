using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Application.Extensions;

public static class ClaimExtensions
{
    public static long? GetUserId(this HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null) return null;
        return long.Parse(userId);
    }
}