using Domain.Entities;
using Domain.Repositories;
using Domain.Specifications.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Specifications.Diagnostics;

public sealed class ExceptionLogsSpecification(IRepository<ExceptionLog> repository)
    : BaseSpecification<ExceptionLog>(repository),
        IExceptionLogsSpecification
{
    public IExceptionLogsSpecification ByFilters(
        string? search = null,
        string? exceptionType = null,
        string? captureKind = null,
        bool? isHandled = null,
        string? requestPath = null,
        string? requestMethod = null,
        DateTimeOffset? occurredFromUtc = null,
        DateTimeOffset? occurredToUtc = null
    )
    {
        var normalizedSearch = search?.Trim();
        var normalizedType = exceptionType?.Trim();
        var normalizedCaptureKind = captureKind?.Trim();
        var normalizedRequestPath = requestPath?.Trim();
        var normalizedRequestMethod = requestMethod?.Trim();

        return (IExceptionLogsSpecification)ApplyCriteria(x =>
            (
                string.IsNullOrWhiteSpace(normalizedSearch)
                || EF.Functions.ILike(x.ExceptionType, $"%{normalizedSearch}%")
                || EF.Functions.ILike(x.Message, $"%{normalizedSearch}%")
                || EF.Functions.ILike(x.TraceId ?? string.Empty, $"%{normalizedSearch}%")
                || EF.Functions.ILike(x.UserIdentifier ?? string.Empty, $"%{normalizedSearch}%")
                || EF.Functions.ILike(x.RequestPath ?? string.Empty, $"%{normalizedSearch}%")
            )
            && (
                string.IsNullOrWhiteSpace(normalizedType)
                || EF.Functions.ILike(x.ExceptionType, $"%{normalizedType}%")
            )
            && (
                string.IsNullOrWhiteSpace(normalizedCaptureKind)
                || EF.Functions.ILike(x.CaptureKind ?? string.Empty, $"%{normalizedCaptureKind}%")
            )
            && (!isHandled.HasValue || x.IsHandled == isHandled.Value)
            && (
                string.IsNullOrWhiteSpace(normalizedRequestPath)
                || EF.Functions.ILike(x.RequestPath ?? string.Empty, $"%{normalizedRequestPath}%")
            )
            && (
                string.IsNullOrWhiteSpace(normalizedRequestMethod)
                || EF.Functions.ILike(x.RequestMethod ?? string.Empty, $"%{normalizedRequestMethod}%")
            )
            && (!occurredFromUtc.HasValue || x.OccurredOnUtc >= occurredFromUtc.Value)
            && (!occurredToUtc.HasValue || x.OccurredOnUtc <= occurredToUtc.Value)
        );
    }

    public IExceptionLogsSpecification OrderByOccurredAt(bool ascending = false)
    {
        return (IExceptionLogsSpecification)ApplyOrder(ascending, x => x.OccurredOnUtc);
    }
}
