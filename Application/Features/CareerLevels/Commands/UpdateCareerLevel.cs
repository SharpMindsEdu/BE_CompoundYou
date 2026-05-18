using Application.Authorization;
using Application.Features.Career.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.CareerLevels.Commands;

public static class UpdateCareerLevel
{
    public const string Endpoint = "api/career-levels/{id:long}";

    public record UpdateCareerLevelCommand(
        long Id,
        decimal Order,
        string Name,
        string? Description
    ) : ICommandRequest<Result<CareerLevelDto>>, IAuditable
    {
        public string AuditAction => "career-level.update";
        public string AuditEntityType => nameof(CareerLevel);
        public long? AuditEntityId => Id;
    }

    public sealed class Validator : AbstractValidator<UpdateCareerLevelCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Order)
                .GreaterThan(0)
                .LessThanOrEqualTo(9999.99m)
                .Must(HaveAtMostTwoDecimalPlaces)
                .WithMessage("Order must have at most two decimal places.");
            RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Description).MaximumLength(1000);
        }

        private static bool HaveAtMostTwoDecimalPlaces(decimal value) =>
            decimal.Round(value, 2) == value;
    }

    internal sealed class Handler(IRepository<CareerLevel> careerLevels)
        : IRequestHandler<UpdateCareerLevelCommand, Result<CareerLevelDto>>
    {
        public async Task<Result<CareerLevelDto>> Handle(UpdateCareerLevelCommand request, CancellationToken ct)
        {
            var level = await careerLevels.GetById(request.Id);
            if (level is null)
                return Result<CareerLevelDto>.Failure("Career level not found", ResultStatus.NotFound);

            if (await careerLevels.Exist(x => x.Id != request.Id && x.JobFamilyId == level.JobFamilyId && x.Order == request.Order, ct))
                return Result<CareerLevelDto>.Failure("Career level order already exists", ResultStatus.Conflict);

            level.Order = request.Order;
            level.Name = request.Name.Trim();
            level.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            careerLevels.Update(level);

            return Result<CareerLevelDto>.Success(
                new CareerLevelDto(level.Id, level.JobFamilyId, level.Order, level.Name, level.Description)
            );
        }
    }
}

public sealed class UpdateCareerLevelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateCareerLevel.Endpoint,
                async (long id, UpdateCareerLevel.UpdateCareerLevelCommand body, ISender sender) =>
                    (await sender.Send(body with { Id = id })).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<CareerLevelDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("UpdateCareerLevel")
            .WithTags("CareerLevels");
    }
}
