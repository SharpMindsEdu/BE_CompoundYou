using Application.Authorization;
using Application.Features.Employees.DTOs;
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

namespace Application.Features.Employees.Commands;

public static class AssignToTeam
{
    public const string Endpoint = "api/employees/{id:long}/team";

    public record AssignToTeamCommand(long Id, long? TeamId)
        : ICommandRequest<Result<EmployeeDto>>, IAuditable
    {
        public string AuditAction => "employee.assign_team";
        public string AuditEntityType => nameof(Employee);
        public long? AuditEntityId => Id;
    }

    internal sealed class Handler(IRepository<Employee> employees, IRepository<Team> teams)
        : IRequestHandler<AssignToTeamCommand, Result<EmployeeDto>>
    {
        public async Task<Result<EmployeeDto>> Handle(AssignToTeamCommand request, CancellationToken ct)
        {
            var employee = await employees.GetById(request.Id);
            if (employee is null)
                return Result<EmployeeDto>.Failure(
                    TenancyErrors.EmployeeNotFound,
                    ResultStatus.NotFound
                );

            if (request.TeamId is not null && !await teams.Exist(t => t.Id == request.TeamId.Value, ct))
                return Result<EmployeeDto>.Failure(TenancyErrors.TeamNotFound, ResultStatus.NotFound);

            employee.TeamId = request.TeamId;
            employee.UpdatedOn = DateTimeOffset.UtcNow;
            employees.Update(employee);
            return Result<EmployeeDto>.Success(employee);
        }
    }
}

public class AssignToTeamEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                AssignToTeam.Endpoint,
                async (long id, AssignToTeamRequest body, ISender sender) =>
                {
                    var result = await sender.Send(new AssignToTeam.AssignToTeamCommand(id, body.TeamId));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<EmployeeDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("AssignEmployeeToTeam")
            .WithTags("Employee");
    }

    public record AssignToTeamRequest(long? TeamId);
}
