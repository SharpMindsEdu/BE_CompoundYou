using Application.Authorization;
using Application.Features.Employees.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Employees.Queries;

public static class GetEmployee
{
    public const string Endpoint = "api/employees/{id:long}";

    public record GetEmployeeQuery(long Id) : IRequest<Result<EmployeeDto>>;

    internal sealed class Handler(IRepository<Employee> employees)
        : IRequestHandler<GetEmployeeQuery, Result<EmployeeDto>>
    {
        public async Task<Result<EmployeeDto>> Handle(GetEmployeeQuery request, CancellationToken ct)
        {
            var employee = await employees.GetById(request.Id);
            if (employee is null)
                return Result<EmployeeDto>.Failure(
                    TenancyErrors.EmployeeNotFound,
                    ResultStatus.NotFound
                );
            return Result<EmployeeDto>.Success(employee.Adapt<EmployeeDto>());
        }
    }
}

public class GetEmployeeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetEmployee.Endpoint,
                async (
                    long id,
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

                    var result = await sender.Send(new GetEmployee.GetEmployeeQuery(id));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<EmployeeDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetEmployee")
            .WithTags("Employee");
    }
}
