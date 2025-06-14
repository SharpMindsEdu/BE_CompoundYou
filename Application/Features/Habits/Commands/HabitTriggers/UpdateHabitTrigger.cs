using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Repositories;
using Carter;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Habits.Commands.HabitTriggers;

public static class UpdateHabitTrigger
{
    public const string Endpoint = "api/habits/{habitId:long}/triggers/{triggerId:long}";

    public record UpdateHabitTriggerCommand(
        long HabitId,
        long TriggerId,
        long? UserId,
        string Title,
        string? Description,
        HabitTriggerType Type,
        long? TriggerHabitId
    ) : ICommandRequest<Result<HabitTrigger>>;

    public class Validator : AbstractValidator<UpdateHabitTriggerCommand>
    {
        public Validator()
        {
            RuleFor(x => x.HabitId).GreaterThanOrEqualTo(0);
            RuleFor(x => x.TriggerId).GreaterThanOrEqualTo(0);
            RuleFor(x => x.UserId).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.Title).NotEmpty().MaximumLength(64);
            RuleFor(x => x.Description).MaximumLength(500);
            RuleFor(x => x.Type).IsInEnum();
        }
    }

    internal sealed class Handler(
        IRepository<HabitTrigger> triggerRepo,
        IRepository<Habit> habitRepo
    ) : IRequestHandler<UpdateHabitTriggerCommand, Result<HabitTrigger>>
    {
        public async Task<Result<HabitTrigger>> Handle(
            UpdateHabitTriggerCommand request,
            CancellationToken ct
        )
        {
            var habit = await habitRepo.GetById(request.HabitId);
            if (habit == null || habit.UserId != request.UserId)
                return Result<HabitTrigger>.Failure(
                    ErrorResults.EntityNotFound,
                    ResultStatus.NotFound
                );

            var trigger = await triggerRepo.GetById(request.TriggerId);
            if (trigger == null || trigger.HabitId != request.HabitId)
                return Result<HabitTrigger>.Failure(
                    "HabitTrigger not found",
                    ResultStatus.NotFound
                );

            if (request.TriggerHabitId.HasValue)
            {
                var triggerHabit = await habitRepo.GetById(request.TriggerHabitId.Value);
                if (triggerHabit == null || triggerHabit.UserId != request.UserId)
                    return Result<HabitTrigger>.Failure(
                        "Trigger habit not found or unauthorized",
                        ResultStatus.BadRequest
                    );
            }

            trigger.Title = request.Title;
            trigger.Description = request.Description;
            trigger.Type = request.Type;
            trigger.TriggerHabitId = request.TriggerHabitId;

            triggerRepo.Update(trigger);
            return Result<HabitTrigger>.Success(trigger);
        }
    }
}

public class UpdateHabitTriggerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateHabitTrigger.Endpoint,
                async (
                    long habitId,
                    long triggerId,
                    UpdateHabitTrigger.UpdateHabitTriggerCommand cmd,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var result = await sender.Send(
                        cmd with
                        {
                            HabitId = habitId,
                            TriggerId = triggerId,
                            UserId = httpContext.GetUserId(),
                        }
                    );

                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<HabitTrigger>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateHabitTrigger")
            .WithTags("HabitTrigger");
    }
}
