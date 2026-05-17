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

public static class AssignManager
{
    public const string Endpoint = "api/employees/{id:long}/manager";
    private const int MaxChainCheckHops = 32;

    public record AssignManagerCommand(long Id, long? ManagerEmployeeId)
        : ICommandRequest<Result<EmployeeDto>>, IAuditable
    {
        public string AuditAction => "employee.assign_manager";
        public string AuditEntityType => nameof(Employee);
        public long? AuditEntityId => Id;
    }

    internal sealed class Handler(IRepository<Employee> employees)
        : IRequestHandler<AssignManagerCommand, Result<EmployeeDto>>
    {
        public async Task<Result<EmployeeDto>> Handle(AssignManagerCommand request, CancellationToken ct)
        {
            var employee = await employees.GetById(request.Id);
            if (employee is null)
                return Result<EmployeeDto>.Failure(
                    TenancyErrors.EmployeeNotFound,
                    ResultStatus.NotFound
                );

            if (request.ManagerEmployeeId is null)
            {
                employee.ManagerEmployeeId = null;
                employee.UpdatedOn = DateTimeOffset.UtcNow;
                employees.Update(employee);
                return Result<EmployeeDto>.Success(employee);
            }

            if (request.ManagerEmployeeId.Value == request.Id)
                return Result<EmployeeDto>.Failure(TenancyErrors.ManagerCycle, ResultStatus.Conflict);

            var manager = await employees.GetById(request.ManagerEmployeeId.Value);
            if (manager is null)
                return Result<EmployeeDto>.Failure(
                    TenancyErrors.EmployeeNotFound,
                    ResultStatus.NotFound
                );

            // Cycle detection: walk up the candidate manager's chain; if we hit the employee, cycle.
            var cursor = manager.ManagerEmployeeId;
            for (var hop = 0; hop < MaxChainCheckHops && cursor is not null; hop++)
            {
                if (cursor.Value == request.Id)
                    return Result<EmployeeDto>.Failure(TenancyErrors.ManagerCycle, ResultStatus.Conflict);
                var upstream = await employees.GetById(cursor.Value);
                cursor = upstream?.ManagerEmployeeId;
            }

            employee.ManagerEmployeeId = request.ManagerEmployeeId;
            employee.UpdatedOn = DateTimeOffset.UtcNow;
            employees.Update(employee);
            return Result<EmployeeDto>.Success(employee);
        }
    }
}

public class AssignManagerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                AssignManager.Endpoint,
                async (long id, AssignManagerRequest body, ISender sender) =>
                {
                    var result = await sender.Send(
                        new AssignManager.AssignManagerCommand(id, body.ManagerEmployeeId)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<EmployeeDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("AssignEmployeeManager")
            .WithTags("Employee");
    }

    public record AssignManagerRequest(long? ManagerEmployeeId);
}
