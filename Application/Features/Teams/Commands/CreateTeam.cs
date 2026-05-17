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

public static class CreateTeam
{
    public const string Endpoint = "api/teams";

    public record CreateTeamCommand(string Name, long DepartmentId, long? ManagerEmployeeId)
        : ICommandRequest<Result<TeamDto>>,
            IAuditable
    {
        public string AuditAction => "team.create";
        public string AuditEntityType => nameof(Team);
        public long? AuditEntityId => null;
    }

    public class Validator : AbstractValidator<CreateTeamCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.DepartmentId).GreaterThan(0);
        }
    }

    internal sealed class Handler(IRepository<Team> teams, IRepository<Department> departments)
        : IRequestHandler<CreateTeamCommand, Result<TeamDto>>
    {
        public async Task<Result<TeamDto>> Handle(CreateTeamCommand request, CancellationToken ct)
        {
            if (!await departments.Exist(d => d.Id == request.DepartmentId, ct))
                return Result<TeamDto>.Failure(
                    TenancyErrors.DepartmentNotFound,
                    ResultStatus.NotFound
                );

            var team = new Team
            {
                Name = request.Name,
                DepartmentId = request.DepartmentId,
                ManagerEmployeeId = request.ManagerEmployeeId,
            };
            await teams.Add(team);
            return Result<TeamDto>.Success(team);
        }
    }
}

public class CreateTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateTeam.Endpoint,
                async (CreateTeam.CreateTeamCommand cmd, ISender sender) =>
                {
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<TeamDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("CreateTeam")
            .WithTags("Team");
    }
}
