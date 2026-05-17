using Application.Authorization;
using Application.Features.Employees.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Employees.Commands;

public static class UpdateEmployee
{
    public const string Endpoint = "api/employees/{id:long}";

    public record UpdateEmployeeCommand(
        long Id,
        string FirstName,
        string LastName,
        string? Email,
        DateOnly? DateOfBirth,
        DateOnly? HireDate
    ) : ICommandRequest<Result<EmployeeDto>>, IAuditable
    {
        public string AuditAction => "employee.update";
        public string AuditEntityType => nameof(Employee);
        public long? AuditEntityId => Id;
    }

    public class Validator : AbstractValidator<UpdateEmployeeCommand>
    {
        public Validator()
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Email).MaximumLength(255).EmailAddress().When(x => x.Email is not null);
        }
    }

    internal sealed class Handler(IRepository<Employee> employees)
        : IRequestHandler<UpdateEmployeeCommand, Result<EmployeeDto>>
    {
        public async Task<Result<EmployeeDto>> Handle(
            UpdateEmployeeCommand request,
            CancellationToken ct
        )
        {
            var employee = await employees.GetById(request.Id);
            if (employee is null)
                return Result<EmployeeDto>.Failure(
                    TenancyErrors.EmployeeNotFound,
                    ResultStatus.NotFound
                );

            employee.FirstName = request.FirstName;
            employee.LastName = request.LastName;
            employee.Email = request.Email;
            employee.DateOfBirth = request.DateOfBirth;
            employee.HireDate = request.HireDate;
            employee.UpdatedOn = DateTimeOffset.UtcNow;
            employees.Update(employee);
            return Result<EmployeeDto>.Success(employee);
        }
    }
}

public class UpdateEmployeeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateEmployee.Endpoint,
                async (
                    long id,
                    UpdateEmployee.UpdateEmployeeCommand body,
                    HttpContext ctx,
                    IAuthorizationService authz,
                    IRepository<Employee> employees,
                    ISender sender
                ) =>
                {
                    var employee = await employees.GetById(id);
                    if (employee is null)
                        return Results.NotFound(TenancyErrors.EmployeeNotFound);

                    var authzResult = await authz.AuthorizeAsync(
                        ctx.User,
                        employee,
                        new EmployeeAccessRequirement()
                    );
                    if (!authzResult.Succeeded)
                        return Results.Forbid();

                    var result = await sender.Send(body with { Id = id });
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<EmployeeDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("UpdateEmployee")
            .WithTags("Employee");
    }
}
