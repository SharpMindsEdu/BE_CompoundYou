using Application.Authorization;
using Application.Features.Career.DTOs;
using Application.Features.CareerPaths.Services;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.CareerPaths.Queries;

public static class GetEmployeeCareerPath
{
    public const string Endpoint = "api/career-paths/employees/{employeeId:long}";

    public record GetEmployeeCareerPathQuery(long EmployeeId, long? TargetRoleProfileId = null)
        : IRequest<Result<CareerPathDto>>;

    internal sealed class Handler(
        IRepository<Employee> employees,
        ICurrentTenant currentTenant,
        IAuthorizationService authService,
        ICareerReadinessService readinessService)
        : IRequestHandler<GetEmployeeCareerPathQuery, Result<CareerPathDto>>
    {
        public async Task<Result<CareerPathDto>> Handle(GetEmployeeCareerPathQuery request, CancellationToken ct)
        {
            var employee = await employees.GetById(request.EmployeeId);
            if (employee is null)
                return Result<CareerPathDto>.Failure("Employee not found", ResultStatus.NotFound);

            var authResult = await authService.AuthorizeAsync(
                currentTenant.User!,
                employee,
                new EmployeeAccessRequirement()
            );
            if (!authResult.Succeeded)
                return Result<CareerPathDto>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);

            var dto = await readinessService.CalculateAsync(
                request.EmployeeId,
                request.TargetRoleProfileId,
                ct
            );
            return Result<CareerPathDto>.Success(dto);
        }
    }
}

public sealed class GetEmployeeCareerPathEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetEmployeeCareerPath.Endpoint,
                async (long employeeId, long? targetRoleProfileId, ISender sender) =>
                    (await sender.Send(
                        new GetEmployeeCareerPath.GetEmployeeCareerPathQuery(employeeId, targetRoleProfileId)
                    )).ToHttpResult()
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<CareerPathDto>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetEmployeeCareerPath")
            .WithTags("CareerPaths");
    }
}
