using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Diagnostics.Queries;

public static class GetExceptionLogById
{
    public const string Endpoint = "api/diagnostics/exceptions/{id:long}";

    public record Query([FromRoute] long Id) : IRequest<Result<ExceptionLogDetailsDto>>;

    public sealed record ExceptionLogDetailsDto(
        long Id,
        DateTimeOffset OccurredOnUtc,
        string ExceptionType,
        string Message,
        string? StackTrace,
        string? Source,
        string? CaptureKind,
        bool IsHandled,
        string? RequestPath,
        string? RequestMethod,
        string? TraceId,
        string? UserIdentifier,
        string? MetadataJson
    );

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }

    internal sealed class Handler(IRepository<ExceptionLog> repository)
        : IRequestHandler<Query, Result<ExceptionLogDetailsDto>>
    {
        public async Task<Result<ExceptionLogDetailsDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var exceptionLog = await repository.GetById(request.Id);
            if (exceptionLog is null)
            {
                return Result<ExceptionLogDetailsDto>.Failure(
                    $"Exception log with id '{request.Id}' was not found.",
                    ResultStatus.NotFound
                );
            }

            return Result<ExceptionLogDetailsDto>.Success(
                new ExceptionLogDetailsDto(
                    exceptionLog.Id,
                    exceptionLog.OccurredOnUtc,
                    exceptionLog.ExceptionType,
                    exceptionLog.Message,
                    exceptionLog.StackTrace,
                    exceptionLog.Source,
                    exceptionLog.CaptureKind,
                    exceptionLog.IsHandled,
                    exceptionLog.RequestPath,
                    exceptionLog.RequestMethod,
                    exceptionLog.TraceId,
                    exceptionLog.UserIdentifier,
                    exceptionLog.MetadataJson
                )
            );
        }
    }
}

public sealed class GetExceptionLogByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetExceptionLogById.Endpoint,
                async ([AsParameters] GetExceptionLogById.Query query, ISender sender) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<GetExceptionLogById.ExceptionLogDetailsDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetExceptionLogById")
            .WithTags("Diagnostics");
    }
}
