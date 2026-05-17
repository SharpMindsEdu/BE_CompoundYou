using Application.Authorization;
using Application.Features.EmployeeSkills.DTOs;
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

namespace Application.Features.EmployeeSkills.Queries;

public static class GetEmployeeMatrix
{
    public const string Endpoint = "api/employee-skills/matrix/{employeeId:long}";

    public record GetEmployeeMatrixQuery(long EmployeeId) : IRequest<Result<List<EmployeeSkillAssessmentDto>>>;

    internal sealed class Handler(
        IRepository<EmployeeSkillAssessment> assessments, 
        IRepository<Employee> employees,
        ICurrentTenant currentTenant,
        IAuthorizationService authService)
        : IRequestHandler<GetEmployeeMatrixQuery, Result<List<EmployeeSkillAssessmentDto>>>
    {
        public async Task<Result<List<EmployeeSkillAssessmentDto>>> Handle(GetEmployeeMatrixQuery request, CancellationToken ct)
        {
            var employee = await employees.GetById(request.EmployeeId);
            if (employee == null)
                return Result<List<EmployeeSkillAssessmentDto>>.Failure("Employee not found", ResultStatus.NotFound);

            var authResult = await authService.AuthorizeAsync(currentTenant.User!, employee, new EmployeeAccessRequirement());
            if (!authResult.Succeeded)
                return Result<List<EmployeeSkillAssessmentDto>>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);

            var list = await assessments.ListAll(a => a.EmployeeId == request.EmployeeId, ct);
            
            var dtos = list.Select(a => new EmployeeSkillAssessmentDto(
                a.Id, a.EmployeeId, a.SkillId, a.ClaimedSkillLevelId, a.ValidatedSkillLevelId, 
                a.ValidatedByEmployeeId, a.ValidatedOn, a.Status, a.Evidence)).ToList();

            return Result<List<EmployeeSkillAssessmentDto>>.Success(dtos);
        }
    }
}

public class GetEmployeeMatrixEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetEmployeeMatrix.Endpoint,
                async (long employeeId, ISender sender) =>
                {
                    var result = await sender.Send(new GetEmployeeMatrix.GetEmployeeMatrixQuery(employeeId));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Manager)
            .Produces<List<EmployeeSkillAssessmentDto>>()
            .WithName("GetEmployeeMatrix")
            .WithTags("EmployeeSkills");
    }
}
