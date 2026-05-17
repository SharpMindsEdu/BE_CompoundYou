using Application.Authorization;
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

public static class ListTenants
{
    public const string Endpoint = "api/tenants";

    public record ListTenantsQuery(int Page = 1, int PageSize = 50) : IRequest<Result<Page<TenantDto>>>;

    internal sealed class Handler(IRepository<Tenant> tenants)
        : IRequestHandler<ListTenantsQuery, Result<Page<TenantDto>>>
    {
        public async Task<Result<Page<TenantDto>>> Handle(ListTenantsQuery request, CancellationToken ct)
        {
            var page = await tenants.ListAllPaged(
                selector: t =>
                    new TenantDto(t.Id, t.Name, t.Slug, t.Status, t.Plan, t.OwnerUserId, t.CreatedOn),
                predicate: null,
                page: request.Page,
                pageSize: request.PageSize,
                cancellationToken: ct
            );
            return Result<Page<TenantDto>>.Success(page);
        }
    }
}

public class ListTenantsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListTenants.Endpoint,
                async (int? page, int? pageSize, ISender sender) =>
                {
                    var result = await sender.Send(
                        new ListTenants.ListTenantsQuery(page ?? 1, pageSize ?? 50)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.PlatformAdmin)
            .Produces<Page<TenantDto>>()
            .WithName("ListTenants")
            .WithTags("Tenant");
    }
}
