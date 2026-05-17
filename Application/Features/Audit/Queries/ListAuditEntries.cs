using System.Linq.Expressions;
using Application.Authorization;
using Application.Features.Audit.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Audit.Queries;

public static class ListAuditEntries
{
    public const string Endpoint = "api/audit";

    public record ListAuditEntriesQuery(
        string? EntityType,
        long? EntityId,
        long? ActorUserId,
        DateTimeOffset? Since,
        DateTimeOffset? Until,
        int Page = 1,
        int PageSize = 50
    ) : IRequest<Result<Page<AuditLogEntryDto>>>;

    internal sealed class Handler(IRepository<AuditLogEntry> repo)
        : IRequestHandler<ListAuditEntriesQuery, Result<Page<AuditLogEntryDto>>>
    {
        public async Task<Result<Page<AuditLogEntryDto>>> Handle(
            ListAuditEntriesQuery request,
            CancellationToken ct
        )
        {
            Expression<Func<AuditLogEntry, bool>> predicate = e =>
                (request.EntityType == null || e.EntityType == request.EntityType)
                && (request.EntityId == null || e.EntityId == request.EntityId)
                && (request.ActorUserId == null || e.ActorUserId == request.ActorUserId)
                && (request.Since == null || e.OccurredOn >= request.Since)
                && (request.Until == null || e.OccurredOn <= request.Until);

            var page = await repo.ListAllPaged(
                selector: e =>
                    new AuditLogEntryDto(
                        e.Id,
                        e.TenantId,
                        e.ActorUserId,
                        e.Action,
                        e.EntityType,
                        e.EntityId,
                        e.OccurredOn,
                        e.MetadataJson
                    ),
                predicate: predicate,
                page: request.Page,
                pageSize: request.PageSize,
                cancellationToken: ct
            );
            return Result<Page<AuditLogEntryDto>>.Success(page);
        }
    }
}

public class ListAuditEntriesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListAuditEntries.Endpoint,
                async (
                    string? entityType,
                    long? entityId,
                    long? actorUserId,
                    DateTimeOffset? since,
                    DateTimeOffset? until,
                    int? page,
                    int? pageSize,
                    ISender sender
                ) =>
                {
                    var result = await sender.Send(
                        new ListAuditEntries.ListAuditEntriesQuery(
                            entityType,
                            entityId,
                            actorUserId,
                            since,
                            until,
                            page ?? 1,
                            pageSize ?? 50
                        )
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<Page<AuditLogEntryDto>>()
            .WithName("ListAuditEntries")
            .WithTags("Audit");
    }
}
