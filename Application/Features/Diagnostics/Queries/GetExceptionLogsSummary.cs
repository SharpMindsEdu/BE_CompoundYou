using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Specifications.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Diagnostics.Queries;

public static class GetExceptionLogsSummary
{
    public const string Endpoint = "api/diagnostics/exceptions/summary";

    public record Query(
        [FromQuery] string? Search = null,
        [FromQuery] string? ExceptionType = null,
        [FromQuery] string? CaptureKind = null,
        [FromQuery] bool? IsHandled = null,
        [FromQuery] string? RequestPath = null,
        [FromQuery] string? RequestMethod = null,
        [FromQuery] DateTimeOffset? OccurredFromUtc = null,
        [FromQuery] DateTimeOffset? OccurredToUtc = null
    ) : IRequest<Result<ExceptionLogsSummaryDto>>;

    public sealed record ExceptionTypeCountDto(string ExceptionType, int Count);

    public sealed record ExceptionLogsSummaryDto(
        int TotalExceptions,
        int HandledExceptions,
        int UnhandledExceptions,
        int DistinctExceptionTypes,
        DateTimeOffset? FirstOccurredOnUtc,
        DateTimeOffset? LastOccurredOnUtc,
        IReadOnlyCollection<ExceptionTypeCountDto> TopExceptionTypes
    );

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.OccurredToUtc)
                .GreaterThanOrEqualTo(x => x.OccurredFromUtc!.Value)
                .When(x => x.OccurredFromUtc.HasValue && x.OccurredToUtc.HasValue)
                .WithMessage("OccurredToUtc must be greater than or equal to OccurredFromUtc.");
        }
    }

    internal sealed class Handler(IExceptionLogsSpecification specification)
        : IRequestHandler<Query, Result<ExceptionLogsSummaryDto>>
    {
        public async Task<Result<ExceptionLogsSummaryDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var exceptionLogs = await specification
                .ByFilters(
                    request.Search,
                    request.ExceptionType,
                    request.CaptureKind,
                    request.IsHandled,
                    request.RequestPath,
                    request.RequestMethod,
                    request.OccurredFromUtc,
                    request.OccurredToUtc
                )
                .OrderByOccurredAt()
                .ToList(cancellationToken);

            var topTypes = exceptionLogs
                .GroupBy(x => x.ExceptionType)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key)
                .Take(10)
                .Select(x => new ExceptionTypeCountDto(x.Key, x.Count()))
                .ToArray();

            var summary = new ExceptionLogsSummaryDto(
                exceptionLogs.Count,
                exceptionLogs.Count(x => x.IsHandled),
                exceptionLogs.Count(x => !x.IsHandled),
                exceptionLogs.Select(x => x.ExceptionType).Distinct(StringComparer.Ordinal).Count(),
                exceptionLogs.MinBy(x => x.OccurredOnUtc)?.OccurredOnUtc,
                exceptionLogs.MaxBy(x => x.OccurredOnUtc)?.OccurredOnUtc,
                topTypes
            );

            return Result<ExceptionLogsSummaryDto>.Success(summary);
        }
    }
}

public sealed class GetExceptionLogsSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetExceptionLogsSummary.Endpoint,
                async ([AsParameters] GetExceptionLogsSummary.Query query, ISender sender) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<GetExceptionLogsSummary.ExceptionLogsSummaryDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetExceptionLogsSummary")
            .WithTags("Diagnostics");
    }
}
