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

namespace Application.Features.Habits.Commands;

public static class UpdateHabit
{
    public const string Endpoint = "api/habits";

    public record UpdateHabitCommand(
        long Id,
        long? UserId,
        string Title,
        int Score,
        string? Description,
        string? Motivation
    ) : ICommandRequest<Result<HabitDto>>;

    public class Validator : AbstractValidator<UpdateHabit.UpdateHabitCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Id).NotNull().Must(x => x > -1);
            RuleFor(x => x.UserId).NotNull().Must(x => x > -1);
            RuleFor(x => x.Title).NotEmpty().MaximumLength(24);
            RuleFor(x => x.Description).MaximumLength(1500);
            RuleFor(x => x.Motivation).MaximumLength(420);
        }
    }

    internal sealed class Handler(IRepository<Habit> repo)
        : IRequestHandler<UpdateHabit.UpdateHabitCommand, Result<HabitDto>>
    {
        public async Task<Result<HabitDto>> Handle(
            UpdateHabit.UpdateHabitCommand request,
            CancellationToken ct
        )
        {
            var existingHabit = await repo.GetByExpression(
                x => x.UserId == request.UserId && x.Id == request.Id,
                ct
            );

            if (existingHabit == null)
                return Result<HabitDto>.Failure(ErrorResults.EntityNotFound);

            existingHabit.Title = request.Title;
            existingHabit.Score = request.Score;
            existingHabit.Description = request.Description;
            existingHabit.Motivation = request.Motivation;

            repo.Update(existingHabit);
            return Result<HabitDto>.Success(existingHabit);
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
                    UpdateHabit.UpdateHabitCommand cmd,
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
            .WithName("UpdateHabit")
            .WithTags("Habit");
    }
}
