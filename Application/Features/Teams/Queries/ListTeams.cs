using Application.Authorization;
using Application.Features.Teams.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Teams.Queries;

public static class ListTeams
{
    public const string Endpoint = "api/teams";

    public record ListTeamsQuery(long? DepartmentId, int Page = 1, int PageSize = 100)
        : IRequest<Result<Page<TeamDto>>>;

    internal sealed class Handler(IRepository<Team> repo)
        : IRequestHandler<ListTeamsQuery, Result<Page<TeamDto>>>
    {
        public async Task<Result<Page<TeamDto>>> Handle(ListTeamsQuery request, CancellationToken ct)
        {
            var page = await repo.ListAllPaged(
                selector: t => new TeamDto(t.Id, t.DepartmentId, t.Name, t.ManagerEmployeeId, t.CreatedOn),
                predicate: request.DepartmentId is null ? null : t => t.DepartmentId == request.DepartmentId.Value,
                page: request.Page,
                pageSize: request.PageSize,
                cancellationToken: ct
            );
            return Result<Page<TeamDto>>.Success(page);
        }
    }
}

public class ListTeamsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListTeams.Endpoint,
                async (long? departmentId, int? page, int? pageSize, ISender sender) =>
                {
                    var result = await sender.Send(
                        new ListTeams.ListTeamsQuery(departmentId, page ?? 1, pageSize ?? 100)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<Page<TeamDto>>()
            .WithName("ListTeams")
            .WithTags("Team");
    }
}
