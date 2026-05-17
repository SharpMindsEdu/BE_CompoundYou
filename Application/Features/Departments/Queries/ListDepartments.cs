using Application.Authorization;
using Application.Features.Departments.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Departments.Queries;

public static class ListDepartments
{
    public const string Endpoint = "api/departments";

    public record ListDepartmentsQuery(int Page = 1, int PageSize = 100) : IRequest<Result<Page<DepartmentDto>>>;

    internal sealed class Handler(IRepository<Department> repo)
        : IRequestHandler<ListDepartmentsQuery, Result<Page<DepartmentDto>>>
    {
        public async Task<Result<Page<DepartmentDto>>> Handle(
            ListDepartmentsQuery request,
            CancellationToken ct
        )
        {
            var page = await repo.ListAllPaged(
                selector: d => new DepartmentDto(d.Id, d.Name, d.ParentDepartmentId, d.CreatedOn),
                predicate: null,
                page: request.Page,
                pageSize: request.PageSize,
                cancellationToken: ct
            );
            return Result<Page<DepartmentDto>>.Success(page);
        }
    }
}

public class ListDepartmentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListDepartments.Endpoint,
                async (int? page, int? pageSize, ISender sender) =>
                {
                    var result = await sender.Send(
                        new ListDepartments.ListDepartmentsQuery(page ?? 1, pageSize ?? 100)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<Page<DepartmentDto>>()
            .WithName("ListDepartments")
            .WithTags("Department");
    }
}
