using Application.Authorization;
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

public static class DeleteTeam
{
    public const string Endpoint = "api/teams/{id:long}";

    public record DeleteTeamCommand(long Id) : ICommandRequest<Result<bool>>, IAuditable
    {
        public string AuditAction => "team.delete";
        public string AuditEntityType => nameof(Team);
        public long? AuditEntityId => Id;
    }

    internal sealed class Handler(IRepository<Team> teams)
        : IRequestHandler<DeleteTeamCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(DeleteTeamCommand request, CancellationToken ct)
        {
            var team = await teams.GetById(request.Id);
            if (team is null)
                return Result<bool>.Failure(TenancyErrors.TeamNotFound, ResultStatus.NotFound);

            teams.Remove(team);
            return Result<bool>.Success(true);
        }
    }
}

public class DeleteTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                DeleteTeam.Endpoint,
                async (long id, ISender sender) =>
                {
                    var result = await sender.Send(new DeleteTeam.DeleteTeamCommand(id));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<bool>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteTeam")
            .WithTags("Team");
    }
}
