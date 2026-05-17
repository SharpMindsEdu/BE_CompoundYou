using Application.Extensions;
using Application.Shared;
using Application.Shared.Extensions;
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
/// Pseudonymizes the calling user's personally identifiable data. Audit
/// log entries authored by the user are kept for compliance but have
/// their <c>ActorUserId</c> cleared. After erasure the user can no longer
/// log in (credentials wiped) and their identifiable fields are
/// replaced with stable placeholders.
/// </summary>
public static class RequestErasure
{
    public const string Endpoint = "api/gdpr/erase";

    public record RequestErasureCommand(long UserId)
        : ICommandRequest<Result<bool>>,
            IAuditable
    {
        public string AuditAction => "gdpr.erase";
        public string AuditEntityType => nameof(User);
        public long? AuditEntityId => UserId;
    }

    internal sealed class Handler(
        IRepository<User> users,
        IRepository<Employee> employees,
        IRepository<TenantMembership> memberships,
        IRepository<AuditLogEntry> auditLog
    ) : IRequestHandler<RequestErasureCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(RequestErasureCommand request, CancellationToken ct)
        {
            var user = await users.GetById(request.UserId);
            if (user is null)
                return Result<bool>.Failure(ErrorResults.UserNotFound, ResultStatus.NotFound);

            var placeholder = $"deleted-user-{user.Id}";
            user.DisplayName = placeholder;
            user.Email = null;
            user.PhoneNumber = null;
            user.SignInSecret = null;
            user.SignInTries = 0;
            user.DeletedOn = DateTimeOffset.UtcNow;
            users.Update(user);

            var employeeRows = await employees.ListAll(e => e.UserId == request.UserId, ct);
            foreach (var employee in employeeRows)
            {
                employee.FirstName = "Deleted";
                employee.LastName = $"User-{user.Id}";
                employee.Email = null;
                employee.DateOfBirth = null;
                employee.IsActive = false;
                employee.DeletedOn = DateTimeOffset.UtcNow;
            }
            employees.Update(employeeRows.ToArray());

            var membershipRows = await memberships.ListAll(m => m.UserId == request.UserId, ct);
            foreach (var membership in membershipRows)
            {
                membership.IsActive = false;
                membership.DeletedOn = DateTimeOffset.UtcNow;
            }
            memberships.Update(membershipRows.ToArray());

            // Audit log entries are retained for compliance but actor is anonymized.
            await auditLog.Update(
                setExpression: s => s.SetProperty(a => a.ActorUserId, (long?)null),
                predicate: a => a.ActorUserId == request.UserId,
                cancellationToken: ct
            );

            return Result<bool>.Success(true);
        }
    }
}

public class RequestErasureEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                RequestErasure.Endpoint,
                async (HttpContext ctx, ISender sender) =>
                {
                    var userId = ctx.GetUserId();
                    if (userId is null)
                        return Results.Unauthorized();
                    var result = await sender.Send(new RequestErasure.RequestErasureCommand(userId.Value));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<bool>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("RequestGdprErasure")
            .WithTags("Gdpr");
    }
}
