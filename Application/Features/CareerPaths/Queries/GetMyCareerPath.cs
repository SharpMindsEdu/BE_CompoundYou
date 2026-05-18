using Application.Authorization;
using Application.Extensions;
using Application.Features.Career.DTOs;
using Application.Features.CareerPaths.Services;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.CareerPaths.Queries;

public static class GetMyCareerPath
{
    public const string Endpoint = "api/career-paths/me";

    public record GetMyCareerPathQuery(long UserId, long? TargetRoleProfileId = null)
        : IRequest<Result<CareerPathDto>>;

    internal sealed class Handler(
        IRepository<Employee> employees,
        ICareerReadinessService readinessService)
        : IRequestHandler<GetMyCareerPathQuery, Result<CareerPathDto>>
    {
        public async Task<Result<CareerPathDto>> Handle(GetMyCareerPathQuery request, CancellationToken ct)
        {
            var employee = await employees.GetByExpression(x => x.UserId == request.UserId, ct);
            if (employee is null)
                return Result<CareerPathDto>.Failure("Employee not found", ResultStatus.NotFound);

            var dto = await readinessService.CalculateAsync(
                employee.Id,
                request.TargetRoleProfileId,
                ct
            );
            return Result<CareerPathDto>.Success(dto);
        }
    }
}

public sealed class GetMyCareerPathEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetMyCareerPath.Endpoint,
                async (long? targetRoleProfileId, HttpContext ctx, ISender sender) =>
                {
                    var userId = ctx.GetUserId();
                    if (userId is null)
                        return Results.Unauthorized();

                    var result = await sender.Send(
                        new GetMyCareerPath.GetMyCareerPathQuery(userId.Value, targetRoleProfileId)
                    );
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<CareerPathDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetMyCareerPath")
            .WithTags("CareerPaths");
    }
}
