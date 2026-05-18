using Application.Authorization;
using Application.Extensions;
using Application.Features.Employees.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Employees.Queries;

public static class GetMyProfile
{
    public const string Endpoint = "api/employees/me";

    public record GetMyProfileQuery(long UserId) : IRequest<Result<EmployeeDto>>;

    internal sealed class Handler(IRepository<Employee> employees, ICurrentTenant currentTenant)
        : IRequestHandler<GetMyProfileQuery, Result<EmployeeDto>>
    {
        public async Task<Result<EmployeeDto>> Handle(GetMyProfileQuery request, CancellationToken ct)
        {
            if (!currentTenant.HasTenant)
                return Result<EmployeeDto>.Failure(
                    TenancyErrors.NoTenantInContext,
                    ResultStatus.Forbidden
                );

            var employee = await employees.GetByExpression(e => e.UserId == request.UserId, ct);
            if (employee is null)
                return Result<EmployeeDto>.Failure(
                    TenancyErrors.EmployeeNotFound,
                    ResultStatus.NotFound
                );
            return Result<EmployeeDto>.Success(employee.Adapt<EmployeeDto>());
        }
    }
}

public class GetMyProfileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetMyProfile.Endpoint,
                async (HttpContext ctx, ISender sender) =>
                {
                    var userId = ctx.GetUserId();
                    if (userId is null)
                        return Results.Unauthorized();
                    var result = await sender.Send(new GetMyProfile.GetMyProfileQuery(userId.Value));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<EmployeeDto>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetMyProfile")
            .WithTags("Employee");
    }
}
