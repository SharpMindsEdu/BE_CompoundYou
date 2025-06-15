using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Repositories;
using Carter;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Habits.Commands;

public static class ExecuteHabit
{
    public const string Endpoint = "api/habit-histories/{id:long}/execute";

    public record ExecuteHabitCommand(long Id, long? UserId, bool IsCompleted, string? Comment)
        : ICommandRequest<Result<HabitHistory>>;

    public class Validator : AbstractValidator<ExecuteHabitCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThanOrEqualTo(0);
        }
    }

    internal sealed class Handler(IRepository<HabitHistory> historyRepo)
        : IRequestHandler<ExecuteHabitCommand, Result<HabitHistory>>
    {
        public async Task<Result<HabitHistory>> Handle(
            ExecuteHabitCommand request,
            CancellationToken ct
        )
        {
            var history = await historyRepo.GetById(request.Id);
            if (history == null || history.UserId != request.UserId)
                return Result<HabitHistory>.Failure(
                    "HabitHistory not found or unauthorized",
                    ResultStatus.NotFound
                );

            // Nur erlauben, wenn Datum innerhalb der letzten 24h
            if (history.Date < DateTime.UtcNow.AddHours(-24) || history.Date > DateTime.UtcNow)
                return Result<HabitHistory>.Failure(
                    "Execution time must be within the last 24 hours",
                    ResultStatus.BadRequest
                );

            history.IsCompleted = request.IsCompleted;
            if (!string.IsNullOrWhiteSpace(request.Comment))
                history.Comment = request.Comment;

            historyRepo.Update(history);

            return Result<HabitHistory>.Success(history);
        }
    }
}

public class ExecuteHabitEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                ExecuteHabit.Endpoint,
                async (
                    long id,
                    ExecuteHabit.ExecuteHabitCommand cmd,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var result = await sender.Send(
                        cmd with
                        {
                            Id = id,
                            UserId = httpContext.GetUserId(),
                        }
                    );

                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("ExecuteHabitHistory")
            .WithTags("HabitHistory");
    }
}
