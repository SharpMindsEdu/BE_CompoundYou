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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Employees.Commands;

public static class CreateEmployee
{
    public const string Endpoint = "api/employees";

    public record CreateEmployeeCommand(
        long UserId,
        string EmployeeNumber,
        string FirstName,
        string LastName,
        string? Email,
        DateOnly? DateOfBirth,
        DateOnly? HireDate,
        long? TeamId,
        long? ManagerEmployeeId,
        string? ExternalSourceId
    ) : ICommandRequest<Result<EmployeeDto>>, IAuditable
    {
        public string AuditAction => "employee.create";
        public string AuditEntityType => nameof(Employee);
        public long? AuditEntityId => null;
    }

    public class Validator : AbstractValidator<CreateEmployeeCommand>
    {
        public Validator()
        {
            RuleFor(x => x.EmployeeNumber).NotEmpty().MaximumLength(64);
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Email).MaximumLength(255).EmailAddress().When(x => x.Email is not null);
            RuleFor(x => x.UserId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IRepository<Employee> employees,
        IRepository<User> users,
        IRepository<Team> teams
    ) : IRequestHandler<CreateEmployeeCommand, Result<EmployeeDto>>
    {
        public async Task<Result<EmployeeDto>> Handle(
            CreateEmployeeCommand request,
            CancellationToken ct
        )
        {
            if (await users.GetById(request.UserId) is null)
                return Result<EmployeeDto>.Failure(ErrorResults.UserNotFound, ResultStatus.NotFound);

            if (await employees.Exist(e => e.EmployeeNumber == request.EmployeeNumber, ct))
                return Result<EmployeeDto>.Failure(
                    TenancyErrors.EmployeeNumberInUse,
                    ResultStatus.Conflict
                );

            if (request.TeamId is not null && !await teams.Exist(t => t.Id == request.TeamId.Value, ct))
                return Result<EmployeeDto>.Failure(TenancyErrors.TeamNotFound, ResultStatus.NotFound);

            if (
                request.ManagerEmployeeId is not null
                && !await employees.Exist(e => e.Id == request.ManagerEmployeeId.Value, ct)
            )
                return Result<EmployeeDto>.Failure(
                    TenancyErrors.EmployeeNotFound,
                    ResultStatus.NotFound
                );

            var employee = new Employee
            {
                UserId = request.UserId,
                EmployeeNumber = request.EmployeeNumber,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                DateOfBirth = request.DateOfBirth,
                HireDate = request.HireDate,
                TeamId = request.TeamId,
                ManagerEmployeeId = request.ManagerEmployeeId,
                ExternalSourceId = request.ExternalSourceId,
                IsActive = true,
            };

            await employees.Add(employee);
            return Result<EmployeeDto>.Success(employee);
        }
    }
}

public class CreateEmployeeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateEmployee.Endpoint,
                async (CreateEmployee.CreateEmployeeCommand cmd, ISender sender) =>
                {
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<EmployeeDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("CreateEmployee")
            .WithTags("Employee");
    }
}
