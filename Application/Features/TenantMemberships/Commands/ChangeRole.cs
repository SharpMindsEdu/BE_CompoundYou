using Application.Authorization;
using Application.Extensions;
using Application.Features.TenantMemberships.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.TenantMemberships.Commands;

public static class ChangeRole
{
    public const string Endpoint = "api/tenants/{tenantId:long}/memberships/{membershipId:long}/role";

    public record ChangeRoleCommand(long TenantId, long MembershipId, TenantRole Role, long? ActorMembershipId)
        : ICommandRequest<Result<TenantMembershipDto>>,
            IAuditable
    {
        public string AuditAction => "tenant_membership.change_role";
        public string AuditEntityType => nameof(TenantMembership);
        public long? AuditEntityId => MembershipId;
    }

    public class Validator : AbstractValidator<ChangeRoleCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Role).IsInEnum();
        }
    }

    internal sealed class Handler(
        IRepository<TenantMembership> memberships,
        ICurrentTenant currentTenant
    ) : IRequestHandler<ChangeRoleCommand, Result<TenantMembershipDto>>
    {
        public async Task<Result<TenantMembershipDto>> Handle(
            ChangeRoleCommand request,
            CancellationToken ct
        )
        {
            if (!currentTenant.IsPlatformAdmin && currentTenant.TenantId != request.TenantId)
                return Result<TenantMembershipDto>.Failure(
                    ErrorResults.Forbidden,
                    ResultStatus.Forbidden
                );

            if (request.ActorMembershipId == request.MembershipId)
                return Result<TenantMembershipDto>.Failure(
                    TenancyErrors.CannotChangeOwnRole,
                    ResultStatus.Conflict
                );

            var membership = await memberships.GetByExpression(
                m => m.Id == request.MembershipId && m.TenantId == request.TenantId,
                ct
            );
            if (membership is null)
                return Result<TenantMembershipDto>.Failure(
                    TenancyErrors.MembershipNotFound,
                    ResultStatus.NotFound
                );

            membership.Role = request.Role;
            membership.UpdatedOn = DateTimeOffset.UtcNow;
            memberships.Update(membership);

            return Result<TenantMembershipDto>.Success(membership);
        }
    }
}

public class ChangeRoleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                ChangeRole.Endpoint,
                async (
                    long tenantId,
                    long membershipId,
                    ChangeRoleRequest body,
                    HttpContext ctx,
                    ISender sender
                ) =>
                {
                    var actorMembershipId = ctx.GetMembershipId();
                    var result = await sender.Send(
                        new ChangeRole.ChangeRoleCommand(tenantId, membershipId, body.Role, actorMembershipId)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<TenantMembershipDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("ChangeMembershipRole")
            .WithTags("TenantMembership");
    }

    public record ChangeRoleRequest(TenantRole Role);
}
