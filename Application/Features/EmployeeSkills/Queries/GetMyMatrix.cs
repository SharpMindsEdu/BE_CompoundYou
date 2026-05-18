using Application.Authorization;
using Application.Features.EmployeeSkills.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.EmployeeSkills.Queries;

public static class GetMyMatrix
{
    public const string Endpoint = "api/employee-skills/my-matrix";

    public record GetMyMatrixQuery() : IRequest<Result<List<EmployeeSkillAssessmentDto>>>;

    internal sealed class Handler(
        IRepository<EmployeeSkillAssessment> assessments,
        IRepository<Employee> employees,
        ICurrentTenant currentTenant)
        : IRequestHandler<GetMyMatrixQuery, Result<List<EmployeeSkillAssessmentDto>>>
    {
        public async Task<Result<List<EmployeeSkillAssessmentDto>>> Handle(GetMyMatrixQuery request, CancellationToken ct)
        {
            if (!currentTenant.UserId.HasValue)
                return Result<List<EmployeeSkillAssessmentDto>>.Failure("User has no employee context", ResultStatus.Forbidden);

            var employee = await employees.GetByExpression(e => e.UserId == currentTenant.UserId.Value, ct);
            if (employee is null)
                return Result<List<EmployeeSkillAssessmentDto>>.Failure("Employee profile not found in current tenant", ResultStatus.NotFound);

            var list = await assessments.ListAll(a => a.EmployeeId == employee.Id, ct);
            
            var dtos = list.Select(a => new EmployeeSkillAssessmentDto(
                a.Id, a.EmployeeId, a.SkillId, a.ClaimedSkillLevelId, a.ValidatedSkillLevelId, 
                a.ValidatedByEmployeeId, a.ValidatedOn, a.Status, a.Evidence)).ToList();

            return Result<List<EmployeeSkillAssessmentDto>>.Success(dtos);
        }
    }
}

public class GetMyMatrixEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetMyMatrix.Endpoint,
                async (ISender sender) =>
                {
                    var result = await sender.Send(new GetMyMatrix.GetMyMatrixQuery());
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<List<EmployeeSkillAssessmentDto>>()
            .WithName("GetMyMatrix")
            .WithTags("EmployeeSkills");
    }
}
