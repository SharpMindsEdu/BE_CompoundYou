using Application.Authorization;
using Application.Features.Teams.DTOs;
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

namespace Application.Features.Teams.Commands;

public static class SetTeamManager
{
    public const string Endpoint = "api/teams/{id:long}/manager";

    public record SetTeamManagerCommand(long Id, long? ManagerEmployeeId)
        : ICommandRequest<Result<TeamDto>>,
            IAuditable
    {
        public string AuditAction => "team.set_manager";
        public string AuditEntityType => nameof(Team);
        public long? AuditEntityId => Id;
    }

    internal sealed class Handler(IRepository<Team> teams, IRepository<Employee> employees)
        : IRequestHandler<SetTeamManagerCommand, Result<TeamDto>>
    {
        public async Task<Result<TeamDto>> Handle(SetTeamManagerCommand request, CancellationToken ct)
        {
            var team = await teams.GetById(request.Id);
            if (team is null)
                return Result<TeamDto>.Failure(TenancyErrors.TeamNotFound, ResultStatus.NotFound);

            if (
                request.ManagerEmployeeId is not null
                && !await employees.Exist(e => e.Id == request.ManagerEmployeeId.Value, ct)
            )
                return Result<TeamDto>.Failure(
                    TenancyErrors.EmployeeNotFound,
                    ResultStatus.NotFound
                );

            team.ManagerEmployeeId = request.ManagerEmployeeId;
            team.UpdatedOn = DateTimeOffset.UtcNow;
            teams.Update(team);
            return Result<TeamDto>.Success(team);
        }
    }
}

public class SetTeamManagerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                SetTeamManager.Endpoint,
                async (long id, SetTeamManagerRequest body, ISender sender) =>
                {
                    var result = await sender.Send(
                        new SetTeamManager.SetTeamManagerCommand(id, body.ManagerEmployeeId)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<TeamDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("SetTeamManager")
            .WithTags("Team");
    }

    public record SetTeamManagerRequest(long? ManagerEmployeeId);
}
