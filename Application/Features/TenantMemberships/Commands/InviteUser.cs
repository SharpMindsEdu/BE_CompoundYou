using System.Security.Cryptography;
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

public static class InviteUser
{
    public const string Endpoint = "api/tenants/{tenantId:long}/invitations";
    public static readonly TimeSpan InviteValidity = TimeSpan.FromDays(7);

    public record InviteUserCommand(long TenantId, string Email, TenantRole Role)
        : ICommandRequest<Result<TenantInvitationDto>>,
            IAuditable
    {
        public string AuditAction => "tenant_invitation.create";
        public string AuditEntityType => nameof(TenantInvitation);
        public long? AuditEntityId => null;
    }

    public class Validator : AbstractValidator<InviteUserCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
            RuleFor(x => x.Role).IsInEnum();
        }
    }

    internal sealed class Handler(
        IRepository<TenantInvitation> invitations,
        IRepository<TenantMembership> memberships,
        IRepository<User> users,
        ICurrentTenant currentTenant
    ) : IRequestHandler<InviteUserCommand, Result<TenantInvitationDto>>
    {
        public async Task<Result<TenantInvitationDto>> Handle(
            InviteUserCommand request,
            CancellationToken ct
        )
        {
            if (!currentTenant.IsPlatformAdmin && currentTenant.TenantId != request.TenantId)
                return Result<TenantInvitationDto>.Failure(
                    ErrorResults.Forbidden,
                    ResultStatus.Forbidden
                );

            // If user already exists with this email and has an active membership, reject
            var existingUser = await users.GetByExpression(u => u.Email == request.Email, ct);
            if (existingUser is not null)
            {
                var existingMembership = await memberships.GetByExpression(
                    m => m.TenantId == request.TenantId && m.UserId == existingUser.Id && m.IsActive,
                    ct
                );
                if (existingMembership is not null)
                    return Result<TenantInvitationDto>.Failure(
                        TenancyErrors.MembershipAlreadyExists,
                        ResultStatus.Conflict
                    );
            }

            var token = GenerateToken();
            var invitation = new TenantInvitation
            {
                TenantId = request.TenantId,
                Email = request.Email,
                Role = request.Role,
                Token = token,
                ExpiresOn = DateTimeOffset.UtcNow.Add(InviteValidity),
            };

            await invitations.Add(invitation);
            return Result<TenantInvitationDto>.Success(invitation);
        }

        private static string GenerateToken()
        {
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}

public class InviteUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                InviteUser.Endpoint,
                async (long tenantId, InviteUser.InviteUserCommand body, ISender sender) =>
                {
                    var result = await sender.Send(body with { TenantId = tenantId });
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<TenantInvitationDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("InviteUser")
            .WithTags("TenantMembership");
    }
}
