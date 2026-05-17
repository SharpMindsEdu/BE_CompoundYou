using Application.Extensions;
using Application.Features.TenantMemberships.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.TenantMemberships.Commands;

public static class AcceptInvite
{
    public const string Endpoint = "api/tenants/invitations/accept";

    public record AcceptInviteCommand(string Token, long UserId)
        : ICommandRequest<Result<TenantMembershipDto>>,
            IAuditable
    {
        public string AuditAction => "tenant_invitation.accept";
        public string AuditEntityType => nameof(TenantInvitation);
        public long? AuditEntityId => null;
    }

    public class Validator : AbstractValidator<AcceptInviteCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.UserId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IRepository<TenantInvitation> invitations,
        IRepository<TenantMembership> memberships
    ) : IRequestHandler<AcceptInviteCommand, Result<TenantMembershipDto>>
    {
        public async Task<Result<TenantMembershipDto>> Handle(
            AcceptInviteCommand request,
            CancellationToken ct
        )
        {
            var invitation = await invitations.GetByExpression(i => i.Token == request.Token, ct);
            if (invitation is null)
                return Result<TenantMembershipDto>.Failure(
                    TenancyErrors.InvitationNotFound,
                    ResultStatus.NotFound
                );
            if (invitation.AcceptedOn is not null)
                return Result<TenantMembershipDto>.Failure(
                    TenancyErrors.InvitationAlreadyAccepted,
                    ResultStatus.Conflict
                );
            if (invitation.ExpiresOn < DateTimeOffset.UtcNow)
                return Result<TenantMembershipDto>.Failure(
                    TenancyErrors.InvitationExpired,
                    ResultStatus.Conflict
                );

            var existing = await memberships.GetByExpression(
                m => m.TenantId == invitation.TenantId && m.UserId == request.UserId,
                ct
            );
            if (existing is not null && existing.IsActive)
                return Result<TenantMembershipDto>.Failure(
                    TenancyErrors.MembershipAlreadyExists,
                    ResultStatus.Conflict
                );

            TenantMembership membership;
            if (existing is null)
            {
                membership = new TenantMembership
                {
                    TenantId = invitation.TenantId,
                    UserId = request.UserId,
                    Role = invitation.Role,
                    IsActive = true,
                };
                await memberships.Add(membership);
            }
            else
            {
                existing.Role = invitation.Role;
                existing.IsActive = true;
                existing.JoinedOn = DateTimeOffset.UtcNow;
                memberships.Update(existing);
                membership = existing;
            }

            invitation.AcceptedOn = DateTimeOffset.UtcNow;
            invitation.AcceptedByUserId = request.UserId;
            invitations.Update(invitation);

            return Result<TenantMembershipDto>.Success(membership);
        }
    }
}

public class AcceptInviteEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                AcceptInvite.Endpoint,
                async (AcceptInviteRequest body, HttpContext ctx, ISender sender) =>
                {
                    var userId = ctx.GetUserId();
                    if (userId is null)
                        return Results.Unauthorized();
                    var result = await sender.Send(
                        new AcceptInvite.AcceptInviteCommand(body.Token, userId.Value)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<TenantMembershipDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("AcceptInvite")
            .WithTags("TenantMembership");
    }

    public record AcceptInviteRequest(string Token);
}
