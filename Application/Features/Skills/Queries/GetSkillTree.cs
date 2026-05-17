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

public static class GetSkillTree
{
    public const string Endpoint = "api/skills/tree";

    public record GetSkillTreeQuery() : IRequest<Result<List<SkillNodeDto>>>;

    internal sealed class Handler(IRepository<Skill> skills)
        : IRequestHandler<GetSkillTreeQuery, Result<List<SkillNodeDto>>>
    {
        public async Task<Result<List<SkillNodeDto>>> Handle(GetSkillTreeQuery request, CancellationToken ct)
        {
            var allSkills = await skills.ListAll(s => s.IsActive, ct);
            
            var roots = allSkills
                .Where(s => s.ParentSkillId == null)
                .Select(s => MapToNode(s, allSkills))
                .ToList();

            return Result<List<SkillNodeDto>>.Success(roots);
        }

        private static SkillNodeDto MapToNode(Skill skill, List<Skill> allSkills)
        {
            var children = allSkills
                .Where(s => s.ParentSkillId == skill.Id)
                .Select(s => MapToNode(s, allSkills))
                .ToList();

            return new SkillNodeDto(skill.Id, skill.Name, skill.Description, children);
        }
    }
}

public class GetSkillTreeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetSkillTree.Endpoint,
                async (ISender sender) =>
                {
                    var result = await sender.Send(new GetSkillTree.GetSkillTreeQuery());
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<List<SkillNodeDto>>()
            .WithName("GetSkillTree")
            .WithTags("Skills");
    }
}
