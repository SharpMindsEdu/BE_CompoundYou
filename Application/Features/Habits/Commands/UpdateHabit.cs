using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Features.Habits.DTOs;
using Application.Repositories;
using Application.Specifications.Habits;
using Carter;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Habits.Commands;

public static class UpdateHabit
{
    public const string Endpoint = "api/habits/{habitId:long}";

    public record UpdateHabitCommand(
        long Id,
        long? UserId,
        string Title,
        int Score,
        string? Description,
        string? Motivation,
        List<HabitTimeDto>? Times = null
    ) : ICommandRequest<Result<HabitDto>>;

    public class Validator : AbstractValidator<UpdateHabitCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Id).NotNull().Must(x => x > -1);
            RuleFor(x => x.UserId).NotNull().Must(x => x > -1);
            RuleFor(x => x.Title).NotEmpty().MaximumLength(24);
            RuleFor(x => x.Description).MaximumLength(1500);
            RuleFor(x => x.Motivation).MaximumLength(420);

            When(
                x => x.Times is not null,
                () =>
                {
                    RuleForEach(x => x.Times!)
                        .ChildRules(times =>
                        {
                            times.RuleFor(t => t.Time).NotEqual(TimeSpan.Zero);
                        });
                }
            );
        }
    }

    internal sealed class Handler(
        IRepository<Habit> repo,
        IGetHabitsWithDetailsSpecification specification
    ) : IRequestHandler<UpdateHabitCommand, Result<HabitDto>>
    {
        public async Task<Result<HabitDto>> Handle(UpdateHabitCommand request, CancellationToken ct)
        {
            var existingHabit = await specification
                .GetHabitWithTimes()
                .ApplyCriteria(x => x.UserId == request.UserId && x.Id == request.Id)
                .FirstOrDefault(ct);

            if (existingHabit == null)
                return Result<HabitDto>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            existingHabit.Title = request.Title;
            existingHabit.Score = request.Score;
            existingHabit.Description = request.Description;
            existingHabit.Motivation = request.Motivation;
            UpdateHabitTimes(request, existingHabit);

            repo.Update(existingHabit);
            return Result<HabitDto>.Success(existingHabit);
        }

        private static void UpdateHabitTimes(UpdateHabitCommand request, Habit existingHabit)
        {
            if (request.Times == null)
            {
                existingHabit.Times.Clear();
                return;
            }

            var incomingTimes = request.Times;
            var incomingIds = incomingTimes.Where(t => t.Id != 0).Select(t => t.Id).ToHashSet();

            existingHabit.Times.RemoveAll(t => t.Id != 0 && !incomingIds.Contains(t.Id));

            foreach (var incoming in incomingTimes)
            {
                if (incoming.Id == 0)
                {
                    existingHabit.Times.Add(
                        new HabitTime { Day = incoming.Day, Time = incoming.Time }
                    );
                    continue;
                }

                var existing = existingHabit.Times.FirstOrDefault(t => t.Id == incoming.Id);
                if (
                    existing is null
                    || (existing.Day == incoming.Day && existing.Time == incoming.Time)
                )
                    continue;

                existing.Day = incoming.Day;
                existing.Time = incoming.Time;
            }
        }
    }
}

public class UpdateHabitEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateHabit.Endpoint,
                async (
                    long habitId,
                    UpdateHabit.UpdateHabitCommand cmd,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var result = await sender.Send(
                        cmd with
                        {
                            UserId = httpContext.GetUserId(),
                            Id = habitId,
                        }
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<HabitDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("UpdateHabit")
            .WithTags("Habit");
    }
}
