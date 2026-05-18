using Application.Authorization;
using Application.Features.Skills.DTOs;
using Application.Features.Skills.Specifications;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Skills.Queries;

public static class ListSkills
{
    public const string Endpoint = "api/skills";

    public record ListSkillsQuery() : IRequest<Result<List<SkillDto>>>;

    internal sealed class Handler(
        IRepository<Skill> skills,
        IRepository<SkillLevel> skillLevels,
        ICurrentTenant currentTenant
    )
        : IRequestHandler<ListSkillsQuery, Result<List<SkillDto>>>
    {
        public async Task<Result<List<SkillDto>>> Handle(ListSkillsQuery request, CancellationToken ct)
        {
            var spec = new SkillsVisibleToTenantSpec();
            var list = await skills.QueryBySpecification(spec, ct);

            var tenantLevelSystem = currentTenant.HasTenant
                ? await skillLevels.ListAll(
                    l => l.SkillId == null && l.TenantId == currentTenant.TenantId && l.IsActive,
                    ct
                )
                : [];
            var levelDtos = tenantLevelSystem
                .OrderBy(l => l.Order)
                .Select(l => new SkillLevelDto(
                    l.Id,
                    l.TenantId,
                    l.Order,
                    l.Name,
                    l.Description,
                    l.PointsThreshold,
                    l.IsActive
                ))
                .ToList();
            
            var dtos = list.Select(s => new SkillDto(
                s.Id,
                s.TenantId,
                s.SkillCategoryId,
                s.Name,
                s.Description,
                s.ParentSkillId,
                s.IsActive,
                levelDtos.ToList()
            )).ToList();

            return Result<List<SkillDto>>.Success(dtos);
        }
    }
}

public class ListSkillsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListSkills.Endpoint,
                async (ISender sender) =>
                {
                    var result = await sender.Send(new ListSkills.ListSkillsQuery());
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<List<SkillDto>>()
            .WithName("ListSkills")
            .WithTags("Skills");
    }
}
