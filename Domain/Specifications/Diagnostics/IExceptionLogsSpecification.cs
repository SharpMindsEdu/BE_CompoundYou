using Domain.Entities;
using Domain.Repositories;

namespace Domain.Specifications.Diagnostics;

public interface IExceptionLogsSpecification : ISpecification<ExceptionLog>
{
    IExceptionLogsSpecification ByFilters(
        string? search = null,
        string? exceptionType = null,
        string? captureKind = null,
        bool? isHandled = null,
        string? requestPath = null,
        string? requestMethod = null,
        DateTimeOffset? occurredFromUtc = null,
        DateTimeOffset? occurredToUtc = null
    );

    IExceptionLogsSpecification OrderByOccurredAt(bool ascending = false);
}
