using Application.Authorization;
using Application.Features.Career.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.RoleProfiles.Queries;

public static class ListRoleProfiles
{
    public const string Endpoint = "api/role-profiles";

    public record ListRoleProfilesQuery(long? JobFamilyId = null, bool? IsActive = null)
        : IRequest<Result<IReadOnlyList<RoleProfileDto>>>;

    internal sealed class Handler(
        IRepository<RoleProfile> roleProfiles,
        IRepository<JobFamily> jobFamilies,
        IRepository<CareerLevel> careerLevels)
        : IRequestHandler<ListRoleProfilesQuery, Result<IReadOnlyList<RoleProfileDto>>>
    {
        public async Task<Result<IReadOnlyList<RoleProfileDto>>> Handle(
            ListRoleProfilesQuery request,
            CancellationToken ct
        )
        {
            var rows = await roleProfiles.ListAll(
                x =>
                    (request.JobFamilyId == null || x.JobFamilyId == request.JobFamilyId.Value)
                    && (request.IsActive == null || x.IsActive == request.IsActive.Value),
                ct
            );
            var familyIds = rows.Select(r => r.JobFamilyId).Distinct().ToArray();
            var levelIds = rows.Select(r => r.CareerLevelId).Distinct().ToArray();
            var families = await jobFamilies.ListAll(x => familyIds.Contains(x.Id), ct);
            var levels = await careerLevels.ListAll(x => levelIds.Contains(x.Id), ct);

            var dto = rows
                .Select(role =>
                {
                    var family = families.FirstOrDefault(x => x.Id == role.JobFamilyId);
                    var level = levels.FirstOrDefault(x => x.Id == role.CareerLevelId);
                    return new RoleProfileDto(
                        role.Id,
                        role.JobFamilyId,
                        family?.Name,
                        role.CareerLevelId,
                        level?.Name,
                        level?.Order,
                        role.Name,
                        role.Description,
                        role.IsActive
                    );
                })
                .OrderBy(x => x.JobFamilyName)
                .ThenBy(x => x.CareerLevelOrder)
                .ThenBy(x => x.Name)
                .ToList();

            return Result<IReadOnlyList<RoleProfileDto>>.Success(dto);
        }
    }
}

public sealed class ListRoleProfilesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListRoleProfiles.Endpoint,
                async (long? jobFamilyId, bool? isActive, ISender sender) =>
                    (await sender.Send(new ListRoleProfiles.ListRoleProfilesQuery(jobFamilyId, isActive))).ToHttpResult()
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<IReadOnlyList<RoleProfileDto>>()
            .WithName("ListRoleProfiles")
            .WithTags("RoleProfiles");
    }
}
