using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Features.Habits.DTOs;
using Application.Repositories;
using Carter;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Habits.Queries;

public static class GetHabits
{
    public const string Endpoint = "api/habits";
    public record GetHabitsQuery(long? UserId) : IRequest<Result<List<HabitDto>>>;
    
    public class Validator : AbstractValidator<GetHabitsQuery>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotNull().Must(x => x > 0);
        }
    }
    
    internal sealed class Handler(
        IRepository<Habit> repo) : IRequestHandler<GetHabitsQuery, Result<List<HabitDto>>>
    {
        public async Task<Result<List<HabitDto>>> Handle(GetHabitsQuery request, CancellationToken ct)
        {
            var habit = await repo.ListAll(x => x.UserId == request.UserId, cancellationToken: ct);
            return Result<List<HabitDto>>.Success(habit);
        }
    }
}

public class GetHabitsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetHabits.Endpoint,
                async (ISender sender, HttpContext httpContext) =>
                {
                    var result = await sender.Send( new GetHabits.GetHabitsQuery( httpContext.GetUserId() ));
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
