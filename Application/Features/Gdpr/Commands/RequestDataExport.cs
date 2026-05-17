using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Application.Extensions;
using Application.Shared;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Gdpr.Commands;

/// <summary>
/// Returns a zip archive containing all data CompoundYou holds about the
/// requesting user (user record, employee profiles across all tenants,
/// tenant memberships, audit-log entries authored by them). Streamed
/// directly in the response; no server-side persistence.
/// </summary>
public static class RequestDataExport
{
    public const string Endpoint = "api/gdpr/export";

    public record RequestDataExportCommand(long UserId)
        : ICommandRequest<Result<DataExportBundle>>,
            IAuditable
    {
        public string AuditAction => "gdpr.export";
        public string AuditEntityType => nameof(User);
        public long? AuditEntityId => UserId;
    }

    public sealed record DataExportBundle(byte[] ZipBytes, string FileName);

    internal sealed class Handler(
        IRepository<User> users,
        IRepository<Employee> employees,
        IRepository<TenantMembership> memberships,
        IRepository<AuditLogEntry> auditLog
    ) : IRequestHandler<RequestDataExportCommand, Result<DataExportBundle>>
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new() { WriteIndented = true };

        public async Task<Result<DataExportBundle>> Handle(
            RequestDataExportCommand request,
            CancellationToken ct
        )
        {
            var user = await users.GetById(request.UserId);
            if (user is null)
                return Result<DataExportBundle>.Failure(
                    ErrorResults.UserNotFound,
                    ResultStatus.NotFound
                );

            // Employee + AuditLogEntry both implement ITenantScoped -> the global
            // query filter normally hides cross-tenant rows. For GDPR export we want
            // every row the user is involved in, regardless of tenant; the user has
            // a right to know.
            var allEmployees = await employees.ListAll(e => e.UserId == request.UserId, ct);
            var allMemberships = await memberships.ListAll(m => m.UserId == request.UserId, ct);
            var allAudit = await auditLog.ListAll(a => a.ActorUserId == request.UserId, ct);

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                await AddEntryAsync(zip, "user.json", user, ct);
                await AddEntryAsync(zip, "employees.json", allEmployees, ct);
                await AddEntryAsync(zip, "memberships.json", allMemberships, ct);
                await AddEntryAsync(zip, "audit-log.json", allAudit, ct);
            }

            var fileName = $"compoundyou-export-{user.Id}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.zip";
            return Result<DataExportBundle>.Success(new DataExportBundle(ms.ToArray(), fileName));
        }

        private static async Task AddEntryAsync<T>(
            ZipArchive zip,
            string name,
            T payload,
            CancellationToken ct
        )
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            await using var stream = entry.Open();
            await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, ct);
        }
    }
}

public class RequestDataExportEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                RequestDataExport.Endpoint,
                async (HttpContext ctx, ISender sender) =>
                {
                    var userId = ctx.GetUserId();
                    if (userId is null)
                        return Results.Unauthorized();
                    var result = await sender.Send(
                        new RequestDataExport.RequestDataExportCommand(userId.Value)
                    );
                    if (!result.Succeeded || result.Data is null)
                        return Results.BadRequest(result.ErrorMessage);
                    return Results.File(
                        result.Data.ZipBytes,
                        "application/zip",
                        result.Data.FileName
                    );
                }
            )
            .RequireAuthorization()
            .Produces<byte[]>(StatusCodes.Status200OK, "application/zip")
            .WithName("RequestGdprExport")
            .WithTags("Gdpr");
    }
}
