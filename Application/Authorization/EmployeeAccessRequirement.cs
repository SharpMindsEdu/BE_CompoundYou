using Microsoft.AspNetCore.Authorization;

namespace Application.Authorization;

/// <summary>
/// Resource-based requirement: caller may access a specific <c>Employee</c>
/// if (a) they are that employee, (b) they are an upstream manager, or
/// (c) they are TenantAdmin / PlatformAdmin. Used with
/// <c>IAuthorizationService.AuthorizeAsync(user, employee, requirement)</c>.
/// </summary>
public sealed class EmployeeAccessRequirement : IAuthorizationRequirement;
