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

public static class UpdateSkill
{
    public const string Endpoint = "api/skills/{id:long}";

    public record UpdateSkillCommand(
        long Id, 
        long SkillCategoryId, 
        string Name, 
        string? Description, 
        long? ParentSkillId, 
        bool IsActive) 
        : IRequest<Result<SkillDto>>, IAuditable
    {
        public string AuditAction => "skill.update";
        public string AuditEntityType => nameof(Skill);
        public long? AuditEntityId => Id;
    }

    internal sealed class Handler(IRepository<Skill> skills, ICurrentTenant currentTenant)
        : IRequestHandler<UpdateSkillCommand, Result<SkillDto>>
    {
        public async Task<Result<SkillDto>> Handle(UpdateSkillCommand request, CancellationToken ct)
        {
            var skill = await skills.GetById(request.Id);
            if (skill == null) 
                return Result<SkillDto>.Failure("Skill not found", ResultStatus.NotFound);

            if (!currentTenant.IsPlatformAdmin)
            {
                if (skill.TenantId == null || skill.TenantId != currentTenant.TenantId)
                    return Result<SkillDto>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);
            }

            skill.SkillCategoryId = request.SkillCategoryId;
            skill.Name = request.Name;
            skill.Description = request.Description;
            skill.ParentSkillId = request.ParentSkillId;
            skill.IsActive = request.IsActive;

            skills.Update(skill);
            await skills.SaveChanges(ct);

            return Result<SkillDto>.Success(new SkillDto(
                skill.Id,
                skill.TenantId,
                skill.SkillCategoryId,
                skill.Name,
                skill.Description,
                skill.ParentSkillId,
                skill.IsActive,
                new List<SkillLevelDto>()
            ));
        }
    }
}

public class UpdateSkillEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateSkill.Endpoint,
                async (long id, UpdateSkill.UpdateSkillCommand command, ISender sender) =>
                {
                    var result = await sender.Send(command with { Id = id });
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<SkillDto>()
            .WithName("UpdateSkill")
            .WithTags("Skills");
    }
}
