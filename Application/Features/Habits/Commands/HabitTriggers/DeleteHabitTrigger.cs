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

namespace Application.Features.Habits.Commands.HabitTriggers;

public static class DeleteHabitTrigger
{
    public const string Endpoint = "api/habits/{habitId:long}/triggers/{triggerId:long}";

    public record DeleteHabitTriggerCommand(long HabitId, long TriggerId, long? UserId)
        : ICommandRequest<Result<bool>>;

    public class Validator : AbstractValidator<DeleteHabitTriggerCommand>
    {
        public Validator()
        {
            RuleFor(x => x.HabitId).GreaterThanOrEqualTo(0);
            RuleFor(x => x.TriggerId).GreaterThanOrEqualTo(0);
            RuleFor(x => x.UserId).NotNull().GreaterThanOrEqualTo(0);
        }
    }

    internal sealed class Handler(
        IRepository<HabitTrigger> triggerRepo,
        IRepository<Habit> habitRepo
    ) : IRequestHandler<DeleteHabitTriggerCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(
            DeleteHabitTriggerCommand request,
            CancellationToken ct
        )
        {
            var habit = await habitRepo.GetById(request.HabitId);
            if (habit == null || habit.UserId != request.UserId)
                return Result<bool>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            var trigger = await triggerRepo.GetById(request.TriggerId);
            if (trigger == null || trigger.HabitId != request.HabitId)
                return Result<bool>.Failure("HabitTrigger not found", ResultStatus.NotFound);

            triggerRepo.Remove(trigger);
            return Result<bool>.Success(true);
        }
    }
}

public class DeleteHabitTriggerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                DeleteHabitTrigger.Endpoint,
                async (long habitId, long triggerId, ISender sender, HttpContext httpContext) =>
                {
                    var cmd = new DeleteHabitTrigger.DeleteHabitTriggerCommand(
                        habitId,
                        triggerId,
                        httpContext.GetUserId()
                    );
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteHabitTrigger")
            .WithTags("HabitTrigger");
    }
}
