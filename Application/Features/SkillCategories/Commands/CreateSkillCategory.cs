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

namespace Application.Features.SkillCategories.Commands;

public static class CreateSkillCategory
{
    public const string Endpoint = "api/skill-categories";

    public record CreateSkillCategoryCommand(string Name, string? Description, bool IsGlobal = false) 
        : IRequest<Result<long>>, IAuditable
    {
        public string AuditAction => "skill_category.create";
        public string AuditEntityType => nameof(SkillCategory);
        public long? AuditEntityId => null;
    }

    internal sealed class Handler(IRepository<SkillCategory> categories, ICurrentTenant currentTenant)
        : IRequestHandler<CreateSkillCategoryCommand, Result<long>>
    {
        public async Task<Result<long>> Handle(CreateSkillCategoryCommand request, CancellationToken ct)
        {
            if (request.IsGlobal && !currentTenant.IsPlatformAdmin)
                return Result<long>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);

            if (!request.IsGlobal && !currentTenant.HasTenant)
                return Result<long>.Failure(TenancyErrors.NoTenantInContext, ResultStatus.Forbidden);

            var category = new SkillCategory
            {
                Name = request.Name,
                Description = request.Description,
                TenantId = request.IsGlobal ? null : currentTenant.TenantId,
                IsActive = true
            };

            await categories.Add(category);
            await categories.SaveChanges(ct);

            return Result<long>.Success(category.Id);
        }
    }
}

public class CreateSkillCategoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateSkillCategory.Endpoint,
                async (CreateSkillCategory.CreateSkillCategoryCommand command, ISender sender, ICurrentTenant currentTenant) =>
                {
                    // Validation: Only PlatformAdmin can create global categories
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
            .WithName("CreateSkillCategory")
            .WithTags("SkillCategories");
    }
}
