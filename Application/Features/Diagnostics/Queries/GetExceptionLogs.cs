using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using Domain.Specifications.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Diagnostics.Queries;

public static class GetExceptionLogs
{
    public const string Endpoint = "api/diagnostics/exceptions";

    public record Query(
        [FromQuery] string? Search = null,
        [FromQuery] string? ExceptionType = null,
        [FromQuery] string? CaptureKind = null,
        [FromQuery] bool? IsHandled = null,
        [FromQuery] string? RequestPath = null,
        [FromQuery] string? RequestMethod = null,
        [FromQuery] DateTimeOffset? OccurredFromUtc = null,
        [FromQuery] DateTimeOffset? OccurredToUtc = null,
        [FromQuery] bool SortAscending = false,
        [FromQuery] int Page = 1,
        [FromQuery] int PageSize = 50
    ) : IRequest<Result<Page<ExceptionLogListItemDto>>>;

    public sealed record ExceptionLogListItemDto(
        long Id,
        DateTimeOffset OccurredOnUtc,
        string ExceptionType,
        string Message,
        string? Source,
        string? CaptureKind,
        bool IsHandled,
        string? RequestPath,
        string? RequestMethod,
        string? TraceId,
        string? UserIdentifier
    );

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Page).GreaterThan(0);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
            RuleFor(x => x.OccurredToUtc)
                .GreaterThanOrEqualTo(x => x.OccurredFromUtc!.Value)
                .When(x => x.OccurredFromUtc.HasValue && x.OccurredToUtc.HasValue)
                .WithMessage("OccurredToUtc must be greater than or equal to OccurredFromUtc.");
        }
    }

    internal sealed class Handler(IExceptionLogsSpecification specification)
        : IRequestHandler<Query, Result<Page<ExceptionLogListItemDto>>>
    {
        public async Task<Result<Page<ExceptionLogListItemDto>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var page = await specification
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
                .OrderByOccurredAt(request.SortAscending)
                .ToPage(request.Page, request.PageSize, cancellationToken);

            var dtoPage = new Page<ExceptionLogListItemDto>(
                page.CurrentPage,
                page.NextPage,
                page.TotalPages,
                page.PageSize,
                page.TotalItems,
                page.Items.Select(Map).ToArray()
            );
            return Result<Page<ExceptionLogListItemDto>>.Success(dtoPage);
        }

        private static ExceptionLogListItemDto Map(ExceptionLog exceptionLog)
        {
            return new ExceptionLogListItemDto(
                exceptionLog.Id,
                exceptionLog.OccurredOnUtc,
                exceptionLog.ExceptionType,
                exceptionLog.Message,
                exceptionLog.Source,
                exceptionLog.CaptureKind,
                exceptionLog.IsHandled,
                exceptionLog.RequestPath,
                exceptionLog.RequestMethod,
                exceptionLog.TraceId,
                exceptionLog.UserIdentifier
            );
        }
    }
}

public sealed class GetExceptionLogsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetExceptionLogs.Endpoint,
                async ([AsParameters] GetExceptionLogs.Query query, ISender sender) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<Page<GetExceptionLogs.ExceptionLogListItemDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetExceptionLogs")
            .WithTags("Diagnostics");
    }
}
