using Application.Extensions;
using Application.Features.Habits.Commands.HabitHistories;
using Application.Features.Habits.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using Domain.Specifications.Habits;
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
        IMediator mediator,
        IRepository<Habit> repo,
        IRepository<HabitHistory> historyRepo,
        IRepository<HabitTime> habitTimeRepo,
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

            var oldHabitTimes = existingHabit
                .Times.Select(t => new HabitTime
                {
                    Id = t.Id,
                    Day = t.Day,
                    Time = t.Time,
                })
                .ToList();

            existingHabit.Title = request.Title;
            existingHabit.Score = request.Score;
            existingHabit.Description = request.Description;
            existingHabit.Motivation = request.Motivation;
            UpdateHabitTimes(request, existingHabit);

            repo.Update(existingHabit);
            await repo.SaveChanges(ct);

            await UpdateHabitHistoriesForTodayAndTomorrow(existingHabit, ct);

            return Result<HabitDto>.Success(existingHabit);
        }

        private void UpdateHabitTimes(UpdateHabitCommand request, Habit existingHabit)
        {
            if (request.Times == null)
            {
                existingHabit.Times.Clear();
                return;
            }

            var incomingTimes = request.Times;
            var incomingIds = incomingTimes.Where(t => t.Id != 0).Select(t => t.Id).ToHashSet();

            var timesToRemove = existingHabit.Times.Where(t =>
                t.Id != 0 && !incomingIds.Contains(t.Id)
            );
            habitTimeRepo.Remove(timesToRemove.ToArray());
            existingHabit.Times.RemoveAll(x => x.Id != 0 && !incomingIds.Contains(x.Id));
            foreach (var incoming in incomingTimes)
            {
                if (incoming.Id == 0)
                {
                    existingHabit.Times.Add(
                        new HabitTime
                        {
                            Day = incoming.Day,
                            Time = incoming.Time,
                            UserId = existingHabit.UserId,
                            HabitId = existingHabit.Id,
                        }
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

        private async Task UpdateHabitHistoriesForTodayAndTomorrow(
            Habit habit,
            CancellationToken ct
        )
        {
            var results = await historyRepo.Remove(
                h =>
                    h.HabitId == habit.Id
                    && h.UserId == habit.UserId
                    && h.Date >= DateTime.UtcNow.Date
                    && !h.IsCompleted,
                ct
            );

            if (habit.Times.Count == 0)
                return;

            await mediator.Send(new CreateHabitHistory.CreateHabitHistoryCommand(habit.UserId), ct);
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
}
