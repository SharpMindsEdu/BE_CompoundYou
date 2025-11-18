using Application.Extensions;
using Application.Features.Habits.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
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
    ) : ICommandRequest<Result<HabitTriggerDto>>;

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
    ) : IRequestHandler<UpdateHabitTriggerCommand, Result<HabitTriggerDto>>
    {
        public async Task<Result<HabitTriggerDto>> Handle(
            UpdateHabitTriggerCommand request,
            CancellationToken ct
        )
        {
            var habit = await habitRepo.GetById(request.HabitId);
            if (habit == null || habit.UserId != request.UserId)
                return Result<HabitTriggerDto>.Failure(
                    ErrorResults.EntityNotFound,
                    ResultStatus.NotFound
                );

            var trigger = await triggerRepo.GetById(request.TriggerId);
            if (trigger == null || trigger.HabitId != request.HabitId)
                return Result<HabitTriggerDto>.Failure(
                    "HabitTrigger not found",
                    ResultStatus.NotFound
                );

            if (request.TriggerHabitId.HasValue)
            {
                var triggerHabit = await habitRepo.GetById(request.TriggerHabitId.Value);
                if (triggerHabit == null || triggerHabit.UserId != request.UserId)
                    return Result<HabitTriggerDto>.Failure(
                        "Trigger habit not found or unauthorized"
                    );
            }

            trigger.Title = request.Title;
            trigger.Description = request.Description;
            trigger.Type = request.Type;
            trigger.TriggerHabitId = request.TriggerHabitId;

            triggerRepo.Update(trigger);
            return Result<HabitTriggerDto>.Success(trigger);
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
            .Produces<HabitTriggerDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateHabitTrigger")
            .WithTags("HabitTrigger");
    }
}
