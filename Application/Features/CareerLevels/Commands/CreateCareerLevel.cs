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

public static class CreateCareerLevel
{
    public const string Endpoint = "api/job-families/{jobFamilyId:long}/levels";

    public record CreateCareerLevelCommand(
        long JobFamilyId,
        decimal Order,
        string Name,
        string? Description
    ) : ICommandRequest<Result<CareerLevelDto>>, IAuditable
    {
        public string AuditAction => "career-level.create";
        public string AuditEntityType => nameof(CareerLevel);
        public long? AuditEntityId => null;
    }

    public sealed class Validator : AbstractValidator<CreateCareerLevelCommand>
    {
        public Validator()
        {
            RuleFor(x => x.JobFamilyId).GreaterThan(0);
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

    internal sealed class Handler(
        IRepository<JobFamily> jobFamilies,
        IRepository<CareerLevel> careerLevels)
        : IRequestHandler<CreateCareerLevelCommand, Result<CareerLevelDto>>
    {
        public async Task<Result<CareerLevelDto>> Handle(CreateCareerLevelCommand request, CancellationToken ct)
        {
            if (!await jobFamilies.Exist(x => x.Id == request.JobFamilyId, ct))
                return Result<CareerLevelDto>.Failure("Job family not found", ResultStatus.NotFound);

            if (await careerLevels.Exist(x => x.JobFamilyId == request.JobFamilyId && x.Order == request.Order, ct))
                return Result<CareerLevelDto>.Failure("Career level order already exists", ResultStatus.Conflict);

            var level = new CareerLevel
            {
                JobFamilyId = request.JobFamilyId,
                Order = request.Order,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            };
            await careerLevels.Add(level);
            return Result<CareerLevelDto>.Success(ToDto(level));
        }
    }

    private static CareerLevelDto ToDto(CareerLevel x) =>
        new(x.Id, x.JobFamilyId, x.Order, x.Name, x.Description);
}

public sealed class CreateCareerLevelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateCareerLevel.Endpoint,
                async (long jobFamilyId, CreateCareerLevel.CreateCareerLevelCommand body, ISender sender) =>
                    (await sender.Send(body with { JobFamilyId = jobFamilyId })).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<CareerLevelDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("CreateCareerLevel")
            .WithTags("CareerLevels");
    }
}
