using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Features.Habits.DTOs;
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

public static class CreateHabitTrigger
{
    public const string Endpoint = "api/habits/{habitId:long}/triggers";

    public record CreateHabitTriggerCommand(
        long HabitId,
        long? UserId,
        string Title,
        string? Description,
        HabitTriggerType Type,
        long? TriggerHabitId
    ) : ICommandRequest<Result<HabitTriggerDto>>;

    public class Validator : AbstractValidator<CreateHabitTriggerCommand>
    {
        public Validator()
        {
            RuleFor(x => x.HabitId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.Title).NotEmpty().MaximumLength(64);
            RuleFor(x => x.Description).MaximumLength(500);
            RuleFor(x => x.Type).IsInEnum();
        }
    }

    internal sealed class Handler(
        IRepository<HabitTrigger> triggerRepo,
        IRepository<Habit> habitRepo
    ) : IRequestHandler<CreateHabitTriggerCommand, Result<HabitTriggerDto>>
    {
        public async Task<Result<HabitTriggerDto>> Handle(
            CreateHabitTriggerCommand request,
            CancellationToken ct
        )
        {
            var habit = await habitRepo.GetById(request.HabitId);
            if (habit == null || habit.UserId != request.UserId)
                return Result<HabitTriggerDto>.Failure(
                    ErrorResults.EntityNotFound,
                    ResultStatus.NotFound
                );

            if (request.TriggerHabitId.HasValue)
            {
                var triggerHabit = await habitRepo.GetById(request.TriggerHabitId.Value);
                if (triggerHabit == null || triggerHabit.UserId != request.UserId)
                    return Result<HabitTriggerDto>.Failure(
                        ErrorResults.EntityNotFound,
                        ResultStatus.NotFound
                    );
            }

            var trigger = new HabitTrigger
            {
                HabitId = request.HabitId,
                TriggerHabitId = request.TriggerHabitId,
                Title = request.Title,
                Description = request.Description,
                Type = request.Type,
            };

            await triggerRepo.Add(trigger);
            return Result<HabitTriggerDto>.Success(trigger);
        }
    }
}

public class CreateHabitTriggerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateHabitTrigger.Endpoint,
                async (
                    long habitId,
                    CreateHabitTrigger.CreateHabitTriggerCommand cmd,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var result = await sender.Send(
                        cmd with
                        {
                            HabitId = habitId,
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
            .WithName("CreateHabitTrigger")
            .WithTags("HabitTrigger");
    }
}
