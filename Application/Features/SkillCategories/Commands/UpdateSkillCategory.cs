using Application.Authorization;
using Application.Features.SkillCategories.DTOs;
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

namespace Application.Features.SkillCategories.Commands;

public static class UpdateSkillCategory
{
    public const string Endpoint = "api/skill-categories/{id:long}";

    public record UpdateSkillCategoryCommand(long Id, string Name, string? Description, bool IsActive) 
        : IRequest<Result<SkillCategoryDto>>, IAuditable
    {
        public string AuditAction => "skill_category.update";
        public string AuditEntityType => nameof(SkillCategory);
        public long? AuditEntityId => Id;
    }

    internal sealed class Handler(IRepository<SkillCategory> categories, ICurrentTenant currentTenant)
        : IRequestHandler<UpdateSkillCategoryCommand, Result<SkillCategoryDto>>
    {
        public async Task<Result<SkillCategoryDto>> Handle(UpdateSkillCategoryCommand request, CancellationToken ct)
        {
            var category = await categories.GetById(request.Id);
            if (category == null) 
                return Result<SkillCategoryDto>.Failure("SkillCategory not found", ResultStatus.NotFound);

            // Access Control: Non-PlatformAdmin cannot update global categories (TenantId == null)
            // and cannot update categories belonging to other tenants.
            if (!currentTenant.IsPlatformAdmin)
            {
                if (category.TenantId == null || category.TenantId != currentTenant.TenantId)
                    return Result<SkillCategoryDto>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);
            }

            category.Name = request.Name;
            category.Description = request.Description;
            category.IsActive = request.IsActive;

            categories.Update(category);
            await categories.SaveChanges(ct);

            return Result<SkillCategoryDto>.Success(new SkillCategoryDto(
                category.Id, category.TenantId, category.Name, category.Description, category.IsActive));
        }
    }
}

public class UpdateSkillCategoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateSkillCategory.Endpoint,
                async (long id, UpdateSkillCategory.UpdateSkillCategoryCommand command, ISender sender) =>
                {
                    var result = await sender.Send(command with { Id = id });
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<SkillCategoryDto>()
            .WithName("UpdateSkillCategory")
            .WithTags("SkillCategories");
    }
}
