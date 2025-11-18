using Application.Extensions;
using Application.Features.Habits.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Specifications.Habits;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Habits.Queries;

public static class GetHabits
{
    public const string Endpoint = "api/habits";

    public record GetHabitsQuery(
        long? UserId,
        bool? IsPreparationHabit = null,
        int? MinScore = null,
        int? MaxScore = null,
        string? Title = null
    ) : IRequest<Result<List<HabitDto>>>;

    public class Validator : AbstractValidator<GetHabitsQuery>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.MinScore).InclusiveBetween(0, 100).When(x => x.MinScore.HasValue);
            RuleFor(x => x.MaxScore).InclusiveBetween(0, 100).When(x => x.MaxScore.HasValue);
            RuleFor(x => x)
                .Must(x => !x.MinScore.HasValue || !x.MaxScore.HasValue || x.MinScore <= x.MaxScore)
                .WithMessage("MinScore darf nicht größer als MaxScore sein.");
        }
    }

    internal sealed class Handler(ISearchHabitsSpecification repo)
        : IRequestHandler<GetHabitsQuery, Result<List<HabitDto>>>
    {
        public async Task<Result<List<HabitDto>>> Handle(
            GetHabitsQuery request,
            CancellationToken ct
        )
        {
            var habits = await repo.ByFilter(
                    request.UserId!.Value,
                    request.IsPreparationHabit,
                    request.MinScore,
                    request.MaxScore,
                    request.Title
                )
                .ToList(ct);
            return Result<List<HabitDto>>.Success(habits);
        }
    }
}

public class GetHabitsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetHabits.Endpoint,
                async (
                    [AsParameters] GetHabits.GetHabitsQuery query,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var userId = httpContext.GetUserId();

                    // Setze die UserId aus dem Token, behalte die restlichen Filter
                    var enrichedQuery = query with
                    {
                        UserId = userId,
                    };

                    var result = await sender.Send(enrichedQuery);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<List<HabitDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("GetHabits")
            .WithTags("Habit");
    }
}
