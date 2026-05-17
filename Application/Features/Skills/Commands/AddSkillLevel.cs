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
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Skills.Commands;

public static class AddSkillLevel
{
    public const string Endpoint = "api/skills/{skillId:long}/levels";

    public record AddSkillLevelCommand(long SkillId, string Name, string? Description, int PointsThreshold) 
        : IRequest<Result<SkillLevelDto>>, IAuditable
    {
        public string AuditAction => "skill_level.add";
        public string AuditEntityType => nameof(SkillLevel);
        public long? AuditEntityId => null;
    }

    internal sealed class Handler(IRepository<Skill> skills, IRepository<SkillLevel> skillLevels, ICurrentTenant currentTenant)
        : IRequestHandler<AddSkillLevelCommand, Result<SkillLevelDto>>
    {
        public async Task<Result<SkillLevelDto>> Handle(AddSkillLevelCommand request, CancellationToken ct)
        {
            var skill = await skills.GetById(request.SkillId);
            if (skill == null)
                return Result<SkillLevelDto>.Failure("Skill not found", ResultStatus.NotFound);

            if (!currentTenant.IsPlatformAdmin)
            {
                if (skill.TenantId == null || skill.TenantId != currentTenant.TenantId)
                    return Result<SkillLevelDto>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);
            }

            var maxOrder = await skillLevels.Count(l => l.SkillId == request.SkillId);

            var level = new SkillLevel
            {
                SkillId = request.SkillId,
                Name = request.Name,
                Description = request.Description,
                PointsThreshold = request.PointsThreshold,
                Order = maxOrder + 1
            };

            await skillLevels.Add(level);
            await skillLevels.SaveChanges(ct);

            return Result<SkillLevelDto>.Success(new SkillLevelDto(
                level.Id, level.SkillId, level.Order, level.Name, level.Description, level.PointsThreshold));
        }
    }
}

public class AddSkillLevelEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                AddSkillLevel.Endpoint,
                async (long skillId, AddSkillLevel.AddSkillLevelCommand command, ISender sender) =>
                {
                    var result = await sender.Send(command with { SkillId = skillId });
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<SkillLevelDto>()
            .WithName("AddSkillLevel")
            .WithTags("Skills");
    }
}
