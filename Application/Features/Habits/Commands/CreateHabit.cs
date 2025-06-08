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

public static class CreateHabit
{
    public const string Endpoint = "api/habits";
    public record CreateHabitCommand(long? UserId, string Title, int Score, string? Description, string? Motivation) : IRequest<Result<HabitDto>>;
    
    public class Validator : AbstractValidator<CreateHabitCommand>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotNull().Must(x => x > 0);
            RuleFor(x => x.Title).NotEmpty().MaximumLength(24);
            RuleFor(x => x.Description).MaximumLength(1500);
            RuleFor(x => x.Motivation).MaximumLength(420);
        }
    }
    
    internal sealed class Handler(
        IRepository<Habit> repo) : IRequestHandler<CreateHabitCommand, Result<HabitDto>>
    {
        public async Task<Result<HabitDto>> Handle(CreateHabitCommand request, CancellationToken ct)
        {
            var habit = new Habit()
            {
                Title = request.Title,
                Description = request.Description,
                Motivation = request.Motivation,
                Score = request.Score,
                UserId = request.UserId!.Value
            };
            await repo.Add(habit);
            await repo.SaveChanges(ct);
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
                async (CreateHabit.CreateHabitCommand cmd, ISender sender, HttpContext httpContext) =>
                {
                    var result = await sender.Send( cmd with { UserId = httpContext.GetUserId() });
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
