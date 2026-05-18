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

public static class CreateJobFamily
{
    public const string Endpoint = "api/job-families";

    public record CreateJobFamilyCommand(string Name, string? Description)
        : ICommandRequest<Result<JobFamilyDto>>,
            IAuditable
    {
        public string AuditAction => "job-family.create";
        public string AuditEntityType => nameof(JobFamily);
        public long? AuditEntityId => null;
    }

    public sealed class Validator : AbstractValidator<CreateJobFamilyCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
            RuleFor(x => x.Description).MaximumLength(1000);
        }
    }

    internal sealed class Handler(IRepository<JobFamily> jobFamilies)
        : IRequestHandler<CreateJobFamilyCommand, Result<JobFamilyDto>>
    {
        public async Task<Result<JobFamilyDto>> Handle(CreateJobFamilyCommand request, CancellationToken ct)
        {
            if (await jobFamilies.Exist(x => x.Name == request.Name, ct))
                return Result<JobFamilyDto>.Failure("Job family name already exists", ResultStatus.Conflict);

            var jobFamily = new JobFamily
            {
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            };
            await jobFamilies.Add(jobFamily);
            return Result<JobFamilyDto>.Success(ToDto(jobFamily));
        }
    }

    private static JobFamilyDto ToDto(JobFamily x) =>
        new(x.Id, x.Name, x.Description, x.IsActive, x.CreatedOn);
}

public sealed class CreateJobFamilyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateJobFamily.Endpoint,
                async (CreateJobFamily.CreateJobFamilyCommand command, ISender sender) =>
                    (await sender.Send(command)).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<JobFamilyDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("CreateJobFamily")
            .WithTags("JobFamilies");
    }
}
