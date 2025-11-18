using Application.Extensions;
using Application.Features.Habits.Commands.HabitHistories;
using Application.Features.Habits.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Habits.Commands;

public static class CreateHabit
{
    public const string Endpoint = "api/habits";

    public record CreateHabitCommand(
        long? UserId,
        string Title,
        int Score,
        string? Description,
        string? Motivation,
        List<HabitTimeDto>? Times = null
    ) : ICommandRequest<Result<HabitDto>>;

    public class Validator : AbstractValidator<CreateHabitCommand>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotNull().GreaterThanOrEqualTo(0);
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

    internal sealed class Handler(IRepository<Habit> repo, IMediator mediator)
        : IRequestHandler<CreateHabitCommand, Result<HabitDto>>
    {
        public async Task<Result<HabitDto>> Handle(CreateHabitCommand request, CancellationToken ct)
        {
            var habit = new Habit()
            {
                Title = request.Title,
                Description = request.Description,
                Motivation = request.Motivation,
                Score = request.Score,
                UserId = request.UserId!.Value,
                Times =
                    request
                        .Times?.Select(t => new HabitTime
                        {
                            UserId = request.UserId!.Value,
                            Day = t.Day,
                            Time = t.Time,
                        })
                        .ToList() ?? [],
            };
            await repo.Add(habit);
            await repo.SaveChanges(ct);
            await mediator.Send(new CreateHabitHistory.CreateHabitHistoryCommand(habit.UserId), ct);
            return Result<HabitDto>.Success(habit);
        }
    }
}

public class CreateHabitEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateHabit.Endpoint,
                async (
                    CreateHabit.CreateHabitCommand cmd,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var result = await sender.Send(cmd with { UserId = httpContext.GetUserId() });
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<HabitDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("CreateHabit")
            .WithTags("Habit");
    }
}
