using Application.Authorization;
using Application.Features.Skills.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Skills.Commands;

public static class ReorderSkillLevels
{
    public const string Endpoint = "api/skills/{skillId:long}/levels/reorder";

    public record ReorderSkillLevelsCommand(long SkillId, List<long> OrderedLevelIds) 
        : IRequest<Result<List<SkillLevelDto>>>, IAuditable
    {
        public string AuditAction => "skill_levels.reorder";
        public string AuditEntityType => nameof(Skill);
        public long? AuditEntityId => SkillId;
    }

    internal sealed class Handler(IRepository<Skill> skills, IRepository<SkillLevel> skillLevels, ICurrentTenant currentTenant)
        : IRequestHandler<ReorderSkillLevelsCommand, Result<List<SkillLevelDto>>>
    {
        public async Task<Result<List<SkillLevelDto>>> Handle(ReorderSkillLevelsCommand request, CancellationToken ct)
        {
            var skill = await skills.GetById(request.SkillId);
            if (skill == null)
                return Result<List<SkillLevelDto>>.Failure("Skill not found", ResultStatus.NotFound);

            if (!currentTenant.IsPlatformAdmin)
            {
                if (skill.TenantId == null || skill.TenantId != currentTenant.TenantId)
                    return Result<List<SkillLevelDto>>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);
            }

            var existingLevels = await skillLevels.ListAll(l => l.SkillId == request.SkillId, ct);
            
            if (request.OrderedLevelIds.Count != existingLevels.Count || 
                request.OrderedLevelIds.Any(id => existingLevels.All(l => l.Id != id)))
            {
                return Result<List<SkillLevelDto>>.Failure("Invalid level IDs provided for reordering", ResultStatus.BadRequest);
            }

            for (int i = 0; i < request.OrderedLevelIds.Count; i++)
            {
                var levelId = request.OrderedLevelIds[i];
                var level = existingLevels.First(l => l.Id == levelId);
                level.Order = i + 1;
                skillLevels.Update(level);
            }

            await skillLevels.SaveChanges(ct);

            var updatedList = existingLevels
                .OrderBy(l => l.Order)
                .Select(l => new SkillLevelDto(l.Id, l.SkillId, l.Order, l.Name, l.Description, l.PointsThreshold))
                .ToList();

            return Result<List<SkillLevelDto>>.Success(updatedList);
        }
    }
}

public class ReorderSkillLevelsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                ReorderSkillLevels.Endpoint,
                async (long skillId, List<long> orderedLevelIds, ISender sender) =>
                {
                    var result = await sender.Send(new ReorderSkillLevels.ReorderSkillLevelsCommand(skillId, orderedLevelIds));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<List<SkillLevelDto>>()
            .WithName("ReorderSkillLevels")
            .WithTags("Skills");
    }
}
