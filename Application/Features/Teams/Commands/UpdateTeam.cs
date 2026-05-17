using Application.Authorization;
using Application.Features.Teams.DTOs;
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

namespace Application.Features.Teams.Commands;

public static class UpdateTeam
{
    public const string Endpoint = "api/teams/{id:long}";

    public record UpdateTeamCommand(long Id, string Name, long DepartmentId)
        : ICommandRequest<Result<TeamDto>>,
            IAuditable
    {
        public string AuditAction => "team.update";
        public string AuditEntityType => nameof(Team);
        public long? AuditEntityId => Id;
    }

    public class Validator : AbstractValidator<UpdateTeamCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.DepartmentId).GreaterThan(0);
        }
    }

    internal sealed class Handler(IRepository<Team> teams, IRepository<Department> departments)
        : IRequestHandler<UpdateTeamCommand, Result<TeamDto>>
    {
        public async Task<Result<TeamDto>> Handle(UpdateTeamCommand request, CancellationToken ct)
        {
            var team = await teams.GetById(request.Id);
            if (team is null)
                return Result<TeamDto>.Failure(TenancyErrors.TeamNotFound, ResultStatus.NotFound);

            if (!await departments.Exist(d => d.Id == request.DepartmentId, ct))
                return Result<TeamDto>.Failure(
                    TenancyErrors.DepartmentNotFound,
                    ResultStatus.NotFound
                );

            team.Name = request.Name;
            team.DepartmentId = request.DepartmentId;
            team.UpdatedOn = DateTimeOffset.UtcNow;
            teams.Update(team);
            return Result<TeamDto>.Success(team);
        }
    }
}

public class UpdateTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateTeam.Endpoint,
                async (long id, UpdateTeam.UpdateTeamCommand body, ISender sender) =>
                {
                    var result = await sender.Send(body with { Id = id });
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<TeamDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("UpdateTeam")
            .WithTags("Team");
    }
}
