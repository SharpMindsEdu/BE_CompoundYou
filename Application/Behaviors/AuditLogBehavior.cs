using Application.Shared;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that writes an audit log entry for commands
/// implementing <see cref="IAuditable"/>. Runs AFTER the handler so
/// failures are not audited as successful actions. The audit row is
/// persisted via <see cref="IAuditLogger"/> which uses the same DbContext;
/// it's flushed by the surrounding <c>TransactionBehavior</c>.
/// </summary>
public sealed class AuditLogBehavior<TRequest, TResponse>(
    IAuditLogger auditLogger,
    ILogger<AuditLogBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        var response = await next();

        if (request is IAuditable auditable)
        {
            try
            {
                await auditLogger.LogAsync(
                    auditable.AuditAction,
                    auditable.AuditEntityType,
                    auditable.AuditEntityId,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to write audit entry for {RequestType}",
                    typeof(TRequest).Name
                );
            }
        }

        return response;
    }
}
