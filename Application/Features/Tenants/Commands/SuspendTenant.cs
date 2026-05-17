using Application.Authorization;
using Application.Features.Tenants.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Tenants.Commands;

public static class SuspendTenant
{
    public const string Endpoint = "api/tenants/{id:long}/suspend";

    public record SuspendTenantCommand(long Id, bool Suspend)
        : ICommandRequest<Result<TenantDto>>,
            IAuditable
    {
        public string AuditAction => Suspend ? "tenant.suspend" : "tenant.reactivate";
        public string AuditEntityType => nameof(Tenant);
        public long? AuditEntityId => Id;
    }

    internal sealed class Handler(IRepository<Tenant> tenants)
        : IRequestHandler<SuspendTenantCommand, Result<TenantDto>>
    {
        public async Task<Result<TenantDto>> Handle(SuspendTenantCommand request, CancellationToken ct)
        {
            var tenant = await tenants.GetById(request.Id);
            if (tenant is null)
                return Result<TenantDto>.Failure(TenancyErrors.TenantNotFound, ResultStatus.NotFound);

            tenant.Status = request.Suspend ? TenantStatus.Suspended : TenantStatus.Active;
            tenant.UpdatedOn = DateTimeOffset.UtcNow;
            tenants.Update(tenant);

            return Result<TenantDto>.Success(tenant);
        }
    }
}

public class SuspendTenantEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                SuspendTenant.Endpoint,
                async (long id, bool suspend, ISender sender) =>
                {
                    var result = await sender.Send(new SuspendTenant.SuspendTenantCommand(id, suspend));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.PlatformAdmin)
            .Produces<TenantDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("SuspendTenant")
            .WithTags("Tenant");
    }
}
