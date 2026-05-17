using System.Linq.Expressions;
using Application.Authorization;
using Application.Features.Employees.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Employees.Queries;

public static class ListEmployees
{
    public const string Endpoint = "api/employees";

    public record ListEmployeesQuery(
        long? TeamId,
        long? ManagerEmployeeId,
        bool? IsActive,
        int Page = 1,
        int PageSize = 50
    ) : IRequest<Result<Page<EmployeeDto>>>;

    internal sealed class Handler(IRepository<Employee> repo)
        : IRequestHandler<ListEmployeesQuery, Result<Page<EmployeeDto>>>
    {
        public async Task<Result<Page<EmployeeDto>>> Handle(
            ListEmployeesQuery request,
            CancellationToken ct
        )
        {
            Expression<Func<Employee, bool>> predicate = e =>
                (request.TeamId == null || e.TeamId == request.TeamId)
                && (request.ManagerEmployeeId == null || e.ManagerEmployeeId == request.ManagerEmployeeId)
                && (request.IsActive == null || e.IsActive == request.IsActive);

            var page = await repo.ListAllPaged(
                selector: e =>
                    new EmployeeDto(
                        e.Id,
                        e.UserId,
                        e.EmployeeNumber,
                        e.FirstName,
                        e.LastName,
                        e.Email,
                        e.DateOfBirth,
                        e.HireDate,
                        e.TeamId,
                        e.ManagerEmployeeId,
                        e.ExternalSourceId,
                        e.IsActive,
                        e.CreatedOn
                    ),
                predicate: predicate,
                page: request.Page,
                pageSize: request.PageSize,
                cancellationToken: ct
            );
            return Result<Page<EmployeeDto>>.Success(page);
        }
    }
}

public class ListEmployeesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListEmployees.Endpoint,
                async (
                    long? teamId,
                    long? managerEmployeeId,
                    bool? isActive,
                    int? page,
                    int? pageSize,
                    ISender sender
                ) =>
                {
                    var result = await sender.Send(
                        new ListEmployees.ListEmployeesQuery(
                            teamId,
                            managerEmployeeId,
                            isActive,
                            page ?? 1,
                            pageSize ?? 50
                        )
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Manager)
            .Produces<Page<EmployeeDto>>()
            .WithName("ListEmployees")
            .WithTags("Employee");
    }
}
