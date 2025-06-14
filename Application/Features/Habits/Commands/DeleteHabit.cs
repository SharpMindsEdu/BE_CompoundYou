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

namespace Application.Features.Habits.Commands;

public static class DeleteHabit
{
    public const string Endpoint = "api/habits/{habitId:long}";

    public record DeleteHabitCommand(long HabitId, long? UserId) : ICommandRequest<Result<bool>>;

    public class Validator : AbstractValidator<DeleteHabitCommand>
    {
        public Validator()
        {
            RuleFor(x => x.HabitId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().Must(x => x > -1);
        }
    }

    internal sealed class Handler(IRepository<Habit> repo)
        : IRequestHandler<DeleteHabitCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(DeleteHabitCommand request, CancellationToken ct)
        {
            var habit = await repo.GetById(request.HabitId);
            if (habit == null || habit.UserId != request.UserId)
                return Result<bool>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            repo.Remove(habit);
            return Result<bool>.Success(true);
        }
    }
}

public class DeleteHabitEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                DeleteHabit.Endpoint,
                async (long habitId, ISender sender, HttpContext httpContext) =>
                {
                    var cmd = new DeleteHabit.DeleteHabitCommand(habitId, httpContext.GetUserId());
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteHabit")
            .WithTags("Habit");
    }
}
