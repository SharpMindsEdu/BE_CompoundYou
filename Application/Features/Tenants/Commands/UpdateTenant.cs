using Application.Authorization;
using Application.Extensions;
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

public static class UpdateTenant
{
    public const string Endpoint = "api/tenants/{id:long}";

    public record UpdateTenantCommand(long Id, string Name, string? Plan)
        : ICommandRequest<Result<TenantDto>>,
            IAuditable
    {
        public string AuditAction => "tenant.update";
        public string AuditEntityType => nameof(Tenant);
        public long? AuditEntityId => Id;
    }

    public class Validator : AbstractValidator<UpdateTenantCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Plan).MaximumLength(80);
        }
    }

    internal sealed class Handler(IRepository<Tenant> tenants, ICurrentTenant currentTenant)
        : IRequestHandler<UpdateTenantCommand, Result<TenantDto>>
    {
        public async Task<Result<TenantDto>> Handle(UpdateTenantCommand request, CancellationToken ct)
        {
            // TenantAdmin may only update their own tenant
            if (!currentTenant.IsPlatformAdmin && currentTenant.TenantId != request.Id)
                return Result<TenantDto>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);

            var tenant = await tenants.GetById(request.Id);
            if (tenant is null)
                return Result<TenantDto>.Failure(TenancyErrors.TenantNotFound, ResultStatus.NotFound);

            tenant.Name = request.Name;
            tenant.Plan = request.Plan;
            tenant.UpdatedOn = DateTimeOffset.UtcNow;
            tenants.Update(tenant);

            return Result<TenantDto>.Success(tenant);
        }
    }
}

public class UpdateTenantEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateTenant.Endpoint,
                async (long id, UpdateTenant.UpdateTenantCommand body, ISender sender) =>
                {
                    var result = await sender.Send(body with { Id = id });
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<TenantDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("UpdateTenant")
            .WithTags("Tenant");
    }
}
