using Application.Authorization;
using Application.Extensions;
using Application.Features.TenantMemberships.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.TenantMemberships.Queries;

public static class ListMembers
{
    public const string Endpoint = "api/tenants/{tenantId:long}/memberships";

    public record ListMembersQuery(long TenantId, int Page = 1, int PageSize = 50)
        : IRequest<Result<Page<TenantMembershipDto>>>;

    internal sealed class Handler(
        IRepository<TenantMembership> memberships,
        ICurrentTenant currentTenant
    ) : IRequestHandler<ListMembersQuery, Result<Page<TenantMembershipDto>>>
    {
        public async Task<Result<Page<TenantMembershipDto>>> Handle(
            ListMembersQuery request,
            CancellationToken ct
        )
        {
            if (!currentTenant.IsPlatformAdmin && currentTenant.TenantId != request.TenantId)
                return Result<Page<TenantMembershipDto>>.Failure(
                    ErrorResults.Forbidden,
                    ResultStatus.Forbidden
                );

            var page = await memberships.ListAllPaged(
                selector: m =>
                    new TenantMembershipDto(m.Id, m.TenantId, m.UserId, m.Role, m.JoinedOn, m.IsActive),
                predicate: m => m.TenantId == request.TenantId,
                page: request.Page,
                pageSize: request.PageSize,
                cancellationToken: ct
            );
            return Result<Page<TenantMembershipDto>>.Success(page);
        }
    }
}

public class ListMembersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListMembers.Endpoint,
                async (long tenantId, int? page, int? pageSize, ISender sender) =>
                {
                    var result = await sender.Send(
                        new ListMembers.ListMembersQuery(tenantId, page ?? 1, pageSize ?? 50)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<Page<TenantMembershipDto>>()
            .WithName("ListMembers")
            .WithTags("TenantMembership");
    }
}
