using Application.Authorization;
using Application.Extensions;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.TenantMemberships.Commands;

public static class RemoveMember
{
    public const string Endpoint = "api/tenants/{tenantId:long}/memberships/{membershipId:long}";

    public record RemoveMemberCommand(long TenantId, long MembershipId, long? ActorMembershipId)
        : ICommandRequest<Result<bool>>,
            IAuditable
    {
        public string AuditAction => "tenant_membership.remove";
        public string AuditEntityType => nameof(TenantMembership);
        public long? AuditEntityId => MembershipId;
    }

    internal sealed class Handler(
        IRepository<TenantMembership> memberships,
        ICurrentTenant currentTenant
    ) : IRequestHandler<RemoveMemberCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(RemoveMemberCommand request, CancellationToken ct)
        {
            if (!currentTenant.IsPlatformAdmin && currentTenant.TenantId != request.TenantId)
                return Result<bool>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);

            if (request.ActorMembershipId == request.MembershipId)
                return Result<bool>.Failure(TenancyErrors.CannotRemoveSelf, ResultStatus.Conflict);

            var membership = await memberships.GetByExpression(
                m => m.Id == request.MembershipId && m.TenantId == request.TenantId,
                ct
            );
            if (membership is null)
                return Result<bool>.Failure(TenancyErrors.MembershipNotFound, ResultStatus.NotFound);

            membership.IsActive = false;
            membership.UpdatedOn = DateTimeOffset.UtcNow;
            memberships.Update(membership);

            return Result<bool>.Success(true);
        }
    }
}

public class RemoveMemberEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                RemoveMember.Endpoint,
                async (long tenantId, long membershipId, HttpContext ctx, ISender sender) =>
                {
                    var actorMembershipId = ctx.GetMembershipId();
                    var result = await sender.Send(
                        new RemoveMember.RemoveMemberCommand(tenantId, membershipId, actorMembershipId)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<bool>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("RemoveMember")
            .WithTags("TenantMembership");
    }
}
