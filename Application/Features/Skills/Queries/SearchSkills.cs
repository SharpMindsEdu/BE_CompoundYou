using Application.Authorization;
using Application.Features.Skills.DTOs;
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

public static class SearchSkills
{
    public const string Endpoint = "api/skills/search";

    public record SearchSkillsQuery(string Term) : IRequest<Result<List<SkillDto>>>;

    internal sealed class Handler(
        IRepository<Skill> skills,
        IRepository<SkillLevel> skillLevels,
        ICurrentTenant currentTenant
    )
        : IRequestHandler<SearchSkillsQuery, Result<List<SkillDto>>>
    {
        public async Task<Result<List<SkillDto>>> Handle(SearchSkillsQuery request, CancellationToken ct)
        {
            var term = request.Term.ToLower();
            
            // Note: Global query filter for TenantId is automatically applied.
            var list = await skills.ListAll(
                predicate: s => s.IsActive && (s.Name.ToLower().Contains(term) || (s.Description != null && s.Description.ToLower().Contains(term))),
                cancellationToken: ct
            );

            var levelDtos = currentTenant.HasTenant
                ? (await skillLevels.ListAll(
                    l => l.SkillId == null && l.TenantId == currentTenant.TenantId && l.IsActive,
                    ct
                ))
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
                .ToList()
                : [];
            
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

public class SearchSkillsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                SearchSkills.Endpoint,
                async (string term, ISender sender) =>
                {
                    var result = await sender.Send(new SearchSkills.SearchSkillsQuery(term));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<List<SkillDto>>()
            .WithName("SearchSkills")
            .WithTags("Skills");
    }
}
