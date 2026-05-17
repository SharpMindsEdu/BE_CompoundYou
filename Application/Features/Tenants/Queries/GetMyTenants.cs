using Application.Extensions;
using Application.Features.Tenants.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Tenants.Queries;

public static class GetMyTenants
{
    public const string Endpoint = "api/tenants/me";

    public record GetMyTenantsQuery(long UserId) : IRequest<Result<IReadOnlyList<TenantOptionDto>>>;

    internal sealed class Handler(IRepository<TenantMembership> memberships, IRepository<Tenant> tenants)
        : IRequestHandler<GetMyTenantsQuery, Result<IReadOnlyList<TenantOptionDto>>>
    {
        public async Task<Result<IReadOnlyList<TenantOptionDto>>> Handle(
            GetMyTenantsQuery request,
            CancellationToken ct
        )
        {
            var userMemberships = await memberships.ListAll(
                m => m.UserId == request.UserId && m.IsActive,
                ct
            );
            if (userMemberships.Count == 0)
                return Result<IReadOnlyList<TenantOptionDto>>.Success(Array.Empty<TenantOptionDto>());

            var tenantIds = userMemberships.Select(m => m.TenantId).ToHashSet();
            var allTenants = await tenants.ListAll(t => tenantIds.Contains(t.Id), ct);
            var lookup = allTenants.ToDictionary(t => t.Id);

            var options = userMemberships
                .Where(m => lookup.ContainsKey(m.TenantId))
                .Select(m =>
                {
                    var t = lookup[m.TenantId];
                    return new TenantOptionDto(t.Id, t.Slug, t.Name, m.Role);
                })
                .ToList();

            return Result<IReadOnlyList<TenantOptionDto>>.Success(options);
        }
    }
}

public class GetMyTenantsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetMyTenants.Endpoint,
                async (HttpContext ctx, ISender sender) =>
                {
                    var userId = ctx.GetUserId();
                    if (userId is null)
                        return Results.Unauthorized();
                    var result = await sender.Send(new GetMyTenants.GetMyTenantsQuery(userId.Value));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<IReadOnlyList<TenantOptionDto>>()
            .WithName("GetMyTenants")
            .WithTags("Tenant");
    }
}
