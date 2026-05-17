using Application.Authorization;
using Application.Extensions;
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

public static class GetMyDirectReports
{
    public const string Endpoint = "api/employees/me/direct-reports";

    public record GetMyDirectReportsQuery(long ActorUserId) : IRequest<Result<IReadOnlyList<EmployeeDto>>>;

    internal sealed class Handler(IRepository<Employee> employees)
        : IRequestHandler<GetMyDirectReportsQuery, Result<IReadOnlyList<EmployeeDto>>>
    {
        public async Task<Result<IReadOnlyList<EmployeeDto>>> Handle(
            GetMyDirectReportsQuery request,
            CancellationToken ct
        )
        {
            var actor = await employees.GetByExpression(e => e.UserId == request.ActorUserId, ct);
            if (actor is null)
                return Result<IReadOnlyList<EmployeeDto>>.Success(Array.Empty<EmployeeDto>());

            var rows = await employees.ListAll(
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
                predicate: e => e.ManagerEmployeeId == actor.Id,
                cancellationToken: ct
            );
            return Result<IReadOnlyList<EmployeeDto>>.Success(rows);
        }
    }
}

public class GetMyDirectReportsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetMyDirectReports.Endpoint,
                async (HttpContext ctx, ISender sender) =>
                {
                    var userId = ctx.GetUserId();
                    if (userId is null)
                        return Results.Unauthorized();
                    var result = await sender.Send(
                        new GetMyDirectReports.GetMyDirectReportsQuery(userId.Value)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Manager)
            .Produces<IReadOnlyList<EmployeeDto>>()
            .WithName("GetMyDirectReports")
            .WithTags("Employee");
    }
}
