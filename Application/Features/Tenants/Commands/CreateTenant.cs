using Application.Authorization;
using Application.Features.Tenants.DTOs;
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

namespace Application.Features.Tenants.Commands;

public static class CreateTenant
{
    public const string Endpoint = "api/tenants";

    public record CreateTenantCommand(string Name, string Slug, string? Plan, long? OwnerUserId)
        : ICommandRequest<Result<TenantDto>>,
            IAuditable
    {
        public string AuditAction => "tenant.create";
        public string AuditEntityType => nameof(Tenant);
        public long? AuditEntityId => null;
    }

    public class Validator : AbstractValidator<CreateTenantCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Slug)
                .NotEmpty()
                .MaximumLength(80)
                .Matches("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$")
                .WithMessage("Slug must be lowercase alphanumeric with optional hyphens.");
            RuleFor(x => x.Plan).MaximumLength(80);
        }
    }

    internal sealed class Handler(IRepository<Tenant> tenants, IRepository<User> users)
        : IRequestHandler<CreateTenantCommand, Result<TenantDto>>
    {
        public async Task<Result<TenantDto>> Handle(CreateTenantCommand request, CancellationToken ct)
        {
            if (await tenants.Exist(t => t.Slug == request.Slug, ct))
                return Result<TenantDto>.Failure(TenancyErrors.SlugAlreadyTaken, ResultStatus.Conflict);

            if (request.OwnerUserId is not null && await users.GetById(request.OwnerUserId.Value) is null)
                return Result<TenantDto>.Failure(ErrorResults.UserNotFound, ResultStatus.NotFound);

            var tenant = new Tenant
            {
                Name = request.Name,
                Slug = request.Slug,
                Plan = request.Plan,
                OwnerUserId = request.OwnerUserId,
                Status = TenantStatus.Active,
            };

            await tenants.Add(tenant);
            await tenants.SaveChanges(ct);

            return Result<TenantDto>.Success(tenant);
        }
    }
}

public class CreateTenantEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateTenant.Endpoint,
                async (CreateTenant.CreateTenantCommand cmd, ISender sender) =>
                {
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.PlatformAdmin)
            .Produces<TenantDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("CreateTenant")
            .WithTags("Tenant");
    }
}
