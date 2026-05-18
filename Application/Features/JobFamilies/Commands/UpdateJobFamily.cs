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

namespace Application.Features.JobFamilies.Commands;

public static class UpdateJobFamily
{
    public const string Endpoint = "api/job-families/{id:long}";

    public record UpdateJobFamilyCommand(long Id, string Name, string? Description, bool IsActive)
        : ICommandRequest<Result<JobFamilyDto>>,
            IAuditable
    {
        public string AuditAction => "job-family.update";
        public string AuditEntityType => nameof(JobFamily);
        public long? AuditEntityId => Id;
    }

    public sealed class Validator : AbstractValidator<UpdateJobFamilyCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
            RuleFor(x => x.Description).MaximumLength(1000);
        }
    }

    internal sealed class Handler(IRepository<JobFamily> jobFamilies)
        : IRequestHandler<UpdateJobFamilyCommand, Result<JobFamilyDto>>
    {
        public async Task<Result<JobFamilyDto>> Handle(UpdateJobFamilyCommand request, CancellationToken ct)
        {
            var jobFamily = await jobFamilies.GetById(request.Id);
            if (jobFamily is null)
                return Result<JobFamilyDto>.Failure("Job family not found", ResultStatus.NotFound);

            if (await jobFamilies.Exist(x => x.Id != request.Id && x.Name == request.Name, ct))
                return Result<JobFamilyDto>.Failure("Job family name already exists", ResultStatus.Conflict);

            jobFamily.Name = request.Name.Trim();
            jobFamily.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
            jobFamily.IsActive = request.IsActive;
            jobFamilies.Update(jobFamily);

            return Result<JobFamilyDto>.Success(
                new JobFamilyDto(jobFamily.Id, jobFamily.Name, jobFamily.Description, jobFamily.IsActive, jobFamily.CreatedOn)
            );
        }
    }
}

public sealed class UpdateJobFamilyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateJobFamily.Endpoint,
                async (long id, UpdateJobFamily.UpdateJobFamilyCommand body, ISender sender) =>
                    (await sender.Send(body with { Id = id })).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<JobFamilyDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("UpdateJobFamily")
            .WithTags("JobFamilies");
    }
}
