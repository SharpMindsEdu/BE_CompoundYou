using Application.Features.Tenants.DTOs;
using Application.Features.Users.DTOs;
using Application.Features.Users.Services;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Users.Commands;

/// <summary>
/// Validates credentials via the configured <see cref="IAuthProvider"/>
/// (OTP today, OIDC later), then resolves tenant context:
///   - 0 active memberships: token issued without a tenant claim
///   - 1 active membership : token bound to that tenant
///   - 2+ active memberships: token issued without a tenant claim; client
///     must call <c>SwitchTenant</c> with one of the returned options
/// </summary>
public static class Login
{
    public const string Endpoint = "api/users/login";

    public record LoginCommand(string Code, string? Email, string? PhoneNumber)
        : ICommandRequest<Result<TokenDto>>;

    public class Validator : AbstractValidator<LoginCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Code).NotEmpty().Length(6);
            RuleFor(x => x.Email)
                .Must(
                    (cmd, email) =>
                        !string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(cmd.PhoneNumber)
                )
                .WithMessage(ValidationErrors.EmailAndPhoneNumberMissing);
        }
    }

    internal sealed class Handler(
        IAuthProvider authProvider,
        IRepository<TenantMembership> memberships,
        IRepository<Tenant> tenants,
        ITokenService tokenService
    ) : IRequestHandler<LoginCommand, Result<TokenDto>>
    {
        public async Task<Result<TokenDto>> Handle(LoginCommand request, CancellationToken ct)
        {
            var authResult = await authProvider.AuthenticateAsync(
                new AuthRequest(request.Email, request.PhoneNumber, request.Code),
                ct
            );
            if (!authResult.Succeeded)
                return Result<TokenDto>.Failure(authResult.ErrorMessage ?? string.Empty, authResult.Status);

            var user = authResult.Data!;
            var activeMemberships = await memberships.ListAll(
                m => m.UserId == user.Id && m.IsActive,
                ct
            );

            return activeMemberships.Count switch
            {
                0 => Result<TokenDto>.Success(new TokenDto(tokenService.CreateToken(user))),
                1 => await IssueForSingleMembershipAsync(user, activeMemberships[0], ct),
                _ => await IssueWithTenantPickerAsync(user, activeMemberships, ct),
            };
        }

        private async Task<Result<TokenDto>> IssueForSingleMembershipAsync(
            User user,
            TenantMembership membership,
            CancellationToken ct
        )
        {
            var tenant = await tenants.GetById(membership.TenantId);
            if (tenant is null)
                return Result<TokenDto>.Failure(TenancyErrors.TenantNotFound, ResultStatus.NotFound);

            var token = tokenService.CreateToken(
                user,
                new TenantContextClaims(membership.TenantId, membership.Id, membership.Role)
            );
            return Result<TokenDto>.Success(new TokenDto(token));
        }

        private async Task<Result<TokenDto>> IssueWithTenantPickerAsync(
            User user,
            IReadOnlyList<TenantMembership> activeMemberships,
            CancellationToken ct
        )
        {
            var tenantIds = activeMemberships.Select(m => m.TenantId).ToHashSet();
            var tenantRows = await tenants.ListAll(t => tenantIds.Contains(t.Id), ct);
            var lookup = tenantRows.ToDictionary(t => t.Id);

            var options = activeMemberships
                .Where(m => lookup.ContainsKey(m.TenantId))
                .Select(m =>
                {
                    var t = lookup[m.TenantId];
                    return new TenantOptionDto(t.Id, t.Slug, t.Name, m.Role);
                })
                .ToList();

            var token = tokenService.CreateToken(user);
            return Result<TokenDto>.Success(
                new TokenDto(token, RequiresTenantSelection: true, AvailableTenants: options)
            );
        }
    }
}

public class LoginEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                Login.Endpoint,
                async (Login.LoginCommand cmd, ISender sender) =>
                {
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .Produces<TokenDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("Login")
            .WithTags("User");
    }
}
