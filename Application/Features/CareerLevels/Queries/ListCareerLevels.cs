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

namespace Application.Features.CareerLevels.Queries;

public static class ListCareerLevels
{
    public const string Endpoint = "api/job-families/{jobFamilyId:long}/levels";

    public record ListCareerLevelsQuery(long JobFamilyId) : IRequest<Result<IReadOnlyList<CareerLevelDto>>>;

    internal sealed class Handler(IRepository<CareerLevel> careerLevels)
        : IRequestHandler<ListCareerLevelsQuery, Result<IReadOnlyList<CareerLevelDto>>>
    {
        public async Task<Result<IReadOnlyList<CareerLevelDto>>> Handle(
            ListCareerLevelsQuery request,
            CancellationToken ct
        )
        {
            var rows = await careerLevels.ListAll(x => x.JobFamilyId == request.JobFamilyId, ct);
            return Result<IReadOnlyList<CareerLevelDto>>.Success(
                rows.OrderBy(x => x.Order)
                    .Select(x => new CareerLevelDto(x.Id, x.JobFamilyId, x.Order, x.Name, x.Description))
                    .ToList()
            );
        }
    }
}

public sealed class ListCareerLevelsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListCareerLevels.Endpoint,
                async (long jobFamilyId, ISender sender) =>
                    (await sender.Send(new ListCareerLevels.ListCareerLevelsQuery(jobFamilyId))).ToHttpResult()
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<IReadOnlyList<CareerLevelDto>>()
            .WithName("ListCareerLevels")
            .WithTags("CareerLevels");
    }
}
