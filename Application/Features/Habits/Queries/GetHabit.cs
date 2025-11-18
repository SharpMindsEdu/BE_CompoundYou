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

public static class GetHabit
{
    public const string Endpoint = "api/habits/{habitId:long}";

    public record GetHabitQuery(long HabitId, long? UserId) : IRequest<Result<HabitDto>>;

    public class Validator : AbstractValidator<GetHabitQuery>
    {
        public Validator()
        {
            RuleFor(x => x.HabitId).NotNull().Must(x => x > 0);
            RuleFor(x => x.UserId).NotNull().Must(x => x > 0);
        }
    }

    internal sealed class Handler(ISearchHabitsSpecification searchHabitSpecification)
        : IRequestHandler<GetHabitQuery, Result<HabitDto>>
    {
        public async Task<Result<HabitDto>> Handle(GetHabitQuery request, CancellationToken ct)
        {
            var habit = await searchHabitSpecification
                .AddIncludes()
                .ApplyCriteria(x => x.Id == request.HabitId && x.UserId == request.UserId)
                .FirstOrDefault(ct);
            return habit == null
                ? Result<HabitDto>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound)
                : Result<HabitDto>.Success(habit);
        }
    }
}

public class GetHabitEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetHabit.Endpoint,
                async (long habitId, ISender sender, HttpContext httpContext) =>
                {
                    var result = await sender.Send(
                        new GetHabit.GetHabitQuery(habitId, httpContext.GetUserId())
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<HabitDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("GetHabit")
            .WithTags("Habit");
    }
}
