using Application.Authorization;
using Application.Features.Career.DTOs;
using Application.Features.RoleProfileSkillRequirements.Commands;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.RoleProfileSkillRequirements.Queries;

public static class ListRoleProfileSkillRequirements
{
    public const string Endpoint = "api/role-profiles/{roleProfileId:long}/skill-requirements";

    public record ListRoleProfileSkillRequirementsQuery(long RoleProfileId)
        : IRequest<Result<IReadOnlyList<RoleProfileRequirementDto>>>;

    internal sealed class Handler(
        IRepository<RoleProfileSkillRequirement> requirements,
        IRepository<Skill> skills,
        IRepository<SkillLevel> skillLevels)
        : IRequestHandler<
            ListRoleProfileSkillRequirementsQuery,
            Result<IReadOnlyList<RoleProfileRequirementDto>>
        >
    {
        public async Task<Result<IReadOnlyList<RoleProfileRequirementDto>>> Handle(
            ListRoleProfileSkillRequirementsQuery request,
            CancellationToken ct
        )
        {
            var rows = await requirements.ListAll(x => x.RoleProfileId == request.RoleProfileId, ct);
            return await BulkSetRoleProfileSkillRequirements.BuildResultAsync(rows, skills, skillLevels, ct);
        }
    }
}

public sealed class ListRoleProfileSkillRequirementsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListRoleProfileSkillRequirements.Endpoint,
                async (long roleProfileId, ISender sender) =>
                    (await sender.Send(
                        new ListRoleProfileSkillRequirements.ListRoleProfileSkillRequirementsQuery(roleProfileId)
                    )).ToHttpResult()
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<IReadOnlyList<RoleProfileRequirementDto>>()
            .WithName("ListRoleProfileSkillRequirements")
            .WithTags("RoleProfileSkillRequirements");
    }
}
