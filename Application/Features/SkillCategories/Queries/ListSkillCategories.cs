using Application.Authorization;
using Application.Features.SkillCategories.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.SkillCategories.Queries;

public static class ListSkillCategories
{
    public const string Endpoint = "api/skill-categories";

    public record ListSkillCategoriesQuery() : IRequest<Result<List<SkillCategoryDto>>>;

    internal sealed class Handler(IRepository<SkillCategory> categories)
        : IRequestHandler<ListSkillCategoriesQuery, Result<List<SkillCategoryDto>>>
    {
        public async Task<Result<List<SkillCategoryDto>>> Handle(ListSkillCategoriesQuery request, CancellationToken ct)
        {
            var list = await categories.ListAll(
                selector: c => new SkillCategoryDto(c.Id, c.TenantId, c.Name, c.Description, c.IsActive),
                predicate: c => c.IsActive,
                cancellationToken: ct
            );
            return Result<List<SkillCategoryDto>>.Success(list);
        }
    }
}

public class ListSkillCategoriesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListSkillCategories.Endpoint,
                async (ISender sender) =>
                {
                    var result = await sender.Send(new ListSkillCategories.ListSkillCategoriesQuery());
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<List<SkillCategoryDto>>()
            .WithName("ListSkillCategories")
            .WithTags("SkillCategories");
    }
}
