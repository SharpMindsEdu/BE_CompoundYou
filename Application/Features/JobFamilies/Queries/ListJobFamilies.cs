using Application.Authorization;
using Application.Features.Career.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.JobFamilies.Queries;

public static class ListJobFamilies
{
    public const string Endpoint = "api/job-families";

    public record ListJobFamiliesQuery(bool? IsActive = null) : IRequest<Result<IReadOnlyList<JobFamilyDto>>>;

    internal sealed class Handler(IRepository<JobFamily> jobFamilies)
        : IRequestHandler<ListJobFamiliesQuery, Result<IReadOnlyList<JobFamilyDto>>>
    {
        public async Task<Result<IReadOnlyList<JobFamilyDto>>> Handle(
            ListJobFamiliesQuery request,
            CancellationToken ct
        )
        {
            var rows = await jobFamilies.ListAll(
                request.IsActive is null ? null : x => x.IsActive == request.IsActive.Value,
                ct
            );
            var dto = rows
                .OrderBy(x => x.Name)
                .Select(x => new JobFamilyDto(x.Id, x.Name, x.Description, x.IsActive, x.CreatedOn))
                .ToList();
            return Result<IReadOnlyList<JobFamilyDto>>.Success(dto);
        }
    }
}

public sealed class ListJobFamiliesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListJobFamilies.Endpoint,
                async (bool? isActive, ISender sender) =>
                    (await sender.Send(new ListJobFamilies.ListJobFamiliesQuery(isActive))).ToHttpResult()
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<IReadOnlyList<JobFamilyDto>>()
            .WithName("ListJobFamilies")
            .WithTags("JobFamilies");
    }
}
