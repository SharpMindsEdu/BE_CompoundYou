using Application.Authorization;
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

public static class CreateSkill
{
    public const string Endpoint = "api/skills";

    public record CreateSkillCommand(
        long SkillCategoryId, 
        string Name, 
        string? Description, 
        long? ParentSkillId = null, 
        bool IsGlobal = false) 
        : IRequest<Result<long>>, IAuditable
    {
        public string AuditAction => "skill.create";
        public string AuditEntityType => nameof(Skill);
        public long? AuditEntityId => null;
    }

    internal sealed class Handler(IRepository<Skill> skills, ICurrentTenant currentTenant)
        : IRequestHandler<CreateSkillCommand, Result<long>>
    {
        public async Task<Result<long>> Handle(CreateSkillCommand request, CancellationToken ct)
        {
            var skill = new Skill
            {
                SkillCategoryId = request.SkillCategoryId,
                Name = request.Name,
                Description = request.Description,
                ParentSkillId = request.ParentSkillId,
                TenantId = request.IsGlobal ? null : currentTenant.TenantId,
                IsActive = true
            };

            await skills.Add(skill);
            await skills.SaveChanges(ct);

            return Result<long>.Success(skill.Id);
        }
    }
}

public class CreateSkillEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateSkill.Endpoint,
                async (CreateSkill.CreateSkillCommand command, ISender sender, ICurrentTenant currentTenant) =>
                {
                    if (command.IsGlobal && !currentTenant.IsPlatformAdmin)
                    {
                        return Results.Forbid();
                    }

                    var result = await sender.Send(command);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<long>()
            .WithName("CreateSkill")
            .WithTags("Skills");
    }
}
