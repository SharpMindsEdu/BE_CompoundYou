using Application.Authorization;
using Application.Features.Career.DTOs;
using Application.Features.CareerPaths.Services;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.CareerPaths.Commands;

public static class CreateCareerPathSnapshot
{
    public const string Endpoint = "api/career-paths/employees/{employeeId:long}/snapshots";

    public record CreateCareerPathSnapshotCommand(long EmployeeId, long? TargetRoleProfileId = null)
        : ICommandRequest<Result<CareerPathDto>>,
            IAuditable
    {
        public string AuditAction => "career-path.snapshot.create";
        public string AuditEntityType => nameof(CareerPathSnapshot);
        public long? AuditEntityId => null;
    }

    internal sealed class Handler(
        ICareerReadinessService readinessService,
        IRepository<CareerPathSnapshot> snapshots)
        : IRequestHandler<CreateCareerPathSnapshotCommand, Result<CareerPathDto>>
    {
        public async Task<Result<CareerPathDto>> Handle(
            CreateCareerPathSnapshotCommand request,
            CancellationToken ct
        )
        {
            var dto = await readinessService.CalculateAsync(
                request.EmployeeId,
                request.TargetRoleProfileId,
                ct
            );

            var snapshot = new CareerPathSnapshot
            {
                EmployeeId = request.EmployeeId,
                CurrentRoleProfileId = dto.CurrentRole?.Id,
                TargetRoleProfileId = dto.TargetRole?.Id,
                ReadinessScore = dto.ReadinessScore,
                SkillFitScore = dto.SkillFitScore,
                ValidationCoverageScore = dto.ValidationCoverageScore,
                GoalCompletionScore = dto.GoalCompletionScore,
                Band = dto.Band,
                ScoredOn = dto.ScoredOn,
            };
            await snapshots.Add(snapshot);

            return Result<CareerPathDto>.Success(dto);
        }
    }
}

public sealed class CreateCareerPathSnapshotEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateCareerPathSnapshot.Endpoint,
                async (
                    long employeeId,
                    CreateCareerPathSnapshot.CreateCareerPathSnapshotCommand body,
                    ISender sender
                ) => (await sender.Send(body with { EmployeeId = employeeId })).ToHttpResult()
            )
            .RequireAuthorization(Policies.Manager)
            .Produces<CareerPathDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("CreateCareerPathSnapshot")
            .WithTags("CareerPaths");
    }
}
