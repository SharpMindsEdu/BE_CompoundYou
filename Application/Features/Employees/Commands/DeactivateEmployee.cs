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

public static class DeactivateEmployee
{
    public const string Endpoint = "api/employees/{id:long}/deactivate";

    public record DeactivateEmployeeCommand(long Id, bool Deactivate)
        : ICommandRequest<Result<EmployeeDto>>,
            IAuditable
    {
        public string AuditAction => Deactivate ? "employee.deactivate" : "employee.reactivate";
        public string AuditEntityType => nameof(Employee);
        public long? AuditEntityId => Id;
    }

    internal sealed class Handler(IRepository<Employee> employees)
        : IRequestHandler<DeactivateEmployeeCommand, Result<EmployeeDto>>
    {
        public async Task<Result<EmployeeDto>> Handle(
            DeactivateEmployeeCommand request,
            CancellationToken ct
        )
        {
            var employee = await employees.GetById(request.Id);
            if (employee is null)
                return Result<EmployeeDto>.Failure(
                    TenancyErrors.EmployeeNotFound,
                    ResultStatus.NotFound
                );

            employee.IsActive = !request.Deactivate;
            employee.UpdatedOn = DateTimeOffset.UtcNow;
            employees.Update(employee);
            return Result<EmployeeDto>.Success(employee);
        }
    }
}

public class DeactivateEmployeeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                DeactivateEmployee.Endpoint,
                async (long id, bool deactivate, ISender sender) =>
                {
                    var result = await sender.Send(
                        new DeactivateEmployee.DeactivateEmployeeCommand(id, deactivate)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<EmployeeDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeactivateEmployee")
            .WithTags("Employee");
    }
}
