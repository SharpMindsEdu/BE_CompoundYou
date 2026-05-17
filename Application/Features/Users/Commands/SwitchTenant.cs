using Application.Extensions;
using Application.Features.Users.DTOs;
using Application.Features.Users.Services;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Users.Commands;

/// <summary>
/// Re-issues a JWT bound to a specific tenant the caller belongs to.
/// Used when a multi-tenant user wants to switch active tenant or when
/// Login returned a tenant picker payload.
/// </summary>
public static class SwitchTenant
{
    public const string Endpoint = "api/users/switch-tenant";

    public record SwitchTenantCommand(long UserId, long TenantId) : ICommandRequest<Result<TokenDto>>;

    public class Validator : AbstractValidator<SwitchTenantCommand>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).GreaterThan(0);
            RuleFor(x => x.TenantId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IRepository<User> users,
        IRepository<TenantMembership> memberships,
        IRepository<Tenant> tenants,
        ITokenService tokenService
    ) : IRequestHandler<SwitchTenantCommand, Result<TokenDto>>
    {
        public async Task<Result<TokenDto>> Handle(SwitchTenantCommand request, CancellationToken ct)
        {
            var user = await users.GetById(request.UserId);
            if (user is null)
                return Result<TokenDto>.Failure(ErrorResults.UserNotFound, ResultStatus.NotFound);

            var membership = await memberships.GetByExpression(
                m => m.UserId == request.UserId && m.TenantId == request.TenantId && m.IsActive,
                ct
            );
            if (membership is null)
                return Result<TokenDto>.Failure(
                    TenancyErrors.MembershipNotFound,
                    ResultStatus.NotFound
                );

            var tenant = await tenants.GetById(request.TenantId);
            if (tenant is null)
                return Result<TokenDto>.Failure(TenancyErrors.TenantNotFound, ResultStatus.NotFound);
            if (tenant.Status == TenantStatus.Suspended)
                return Result<TokenDto>.Failure(TenancyErrors.TenantSuspended, ResultStatus.Forbidden);

            var token = tokenService.CreateToken(
                user,
                new TenantContextClaims(membership.TenantId, membership.Id, membership.Role)
            );
            return Result<TokenDto>.Success(new TokenDto(token));
        }
    }
}

public class SwitchTenantEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                SwitchTenant.Endpoint,
                async (SwitchTenantRequest body, HttpContext ctx, ISender sender) =>
                {
                    var userId = ctx.GetUserId();
                    if (userId is null)
                        return Results.Unauthorized();
                    var result = await sender.Send(
                        new SwitchTenant.SwitchTenantCommand(userId.Value, body.TenantId)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<TokenDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("SwitchTenant")
            .WithTags("User");
    }

    public record SwitchTenantRequest(long TenantId);
}
