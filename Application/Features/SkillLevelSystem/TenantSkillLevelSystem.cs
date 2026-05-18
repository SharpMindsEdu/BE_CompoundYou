using Application.Authorization;
using Application.Features.Skills.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.SkillLevelSystem;

public static class TenantSkillLevelSystem
{
    public const string Endpoint = "api/skill-level-system";

    public record SkillLevelSystemInput(string Name, string? Description, int PointsThreshold);

    public record GetTenantSkillLevelSystemQuery() : IRequest<Result<IReadOnlyList<SkillLevelDto>>>;

    public record SetTenantSkillLevelSystemCommand(IReadOnlyList<SkillLevelSystemInput> Levels)
        : IRequest<Result<IReadOnlyList<SkillLevelDto>>>,
            IAuditable
    {
        public string AuditAction => "skill_level_system.set";
        public string AuditEntityType => nameof(SkillLevel);
        public long? AuditEntityId => null;
    }

    public sealed class Validator : AbstractValidator<SetTenantSkillLevelSystemCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Levels).NotEmpty();
            RuleForEach(x => x.Levels).ChildRules(level =>
            {
                level.RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
                level.RuleFor(x => x.Description).MaximumLength(2000);
                level.RuleFor(x => x.PointsThreshold).GreaterThanOrEqualTo(0);
            });
        }
    }

    internal sealed class GetHandler(
        IRepository<SkillLevel> skillLevels,
        ICurrentTenant currentTenant
    ) : IRequestHandler<GetTenantSkillLevelSystemQuery, Result<IReadOnlyList<SkillLevelDto>>>
    {
        public async Task<Result<IReadOnlyList<SkillLevelDto>>> Handle(
            GetTenantSkillLevelSystemQuery request,
            CancellationToken ct
        )
        {
            if (!currentTenant.HasTenant)
                return Result<IReadOnlyList<SkillLevelDto>>.Failure(
                    TenancyErrors.NoTenantInContext,
                    ResultStatus.Forbidden
                );

            var levels = await skillLevels.ListAll(
                x => x.SkillId == null && x.TenantId == currentTenant.TenantId && x.IsActive,
                ct
            );

            return Result<IReadOnlyList<SkillLevelDto>>.Success(Map(levels));
        }
    }

    internal sealed class SetHandler(
        IRepository<SkillLevel> skillLevels,
        ICurrentTenant currentTenant
    ) : IRequestHandler<SetTenantSkillLevelSystemCommand, Result<IReadOnlyList<SkillLevelDto>>>
    {
        public async Task<Result<IReadOnlyList<SkillLevelDto>>> Handle(
            SetTenantSkillLevelSystemCommand request,
            CancellationToken ct
        )
        {
            if (!currentTenant.HasTenant)
                return Result<IReadOnlyList<SkillLevelDto>>.Failure(
                    TenancyErrors.NoTenantInContext,
                    ResultStatus.Forbidden
                );

            var existing = await skillLevels.ListAll(
                x => x.SkillId == null && x.TenantId == currentTenant.TenantId,
                ct
            );

            var activeLevels = new List<SkillLevel>();
            for (var index = 0; index < request.Levels.Count; index++)
            {
                var order = index + 1;
                var input = request.Levels[index];
                var level = existing.FirstOrDefault(x => x.Order == order);

                if (level is null)
                {
                    level = new SkillLevel
                    {
                        TenantId = currentTenant.TenantId,
                        SkillId = null,
                        Order = order,
                        Name = input.Name,
                        Description = input.Description,
                        PointsThreshold = input.PointsThreshold,
                        IsActive = true,
                    };
                    await skillLevels.Add(level);
                }
                else
                {
                    level.Name = input.Name;
                    level.Description = input.Description;
                    level.PointsThreshold = input.PointsThreshold;
                    level.IsActive = true;
                    skillLevels.Update(level);
                }

                activeLevels.Add(level);
            }

            foreach (var stale in existing.Where(x => x.Order > request.Levels.Count && x.IsActive))
            {
                stale.IsActive = false;
                skillLevels.Update(stale);
            }

            await skillLevels.SaveChanges(ct);
            return Result<IReadOnlyList<SkillLevelDto>>.Success(Map(activeLevels));
        }
    }

    private static IReadOnlyList<SkillLevelDto> Map(IEnumerable<SkillLevel> levels) =>
        levels
            .Where(x => x.IsActive)
            .OrderBy(x => x.Order)
            .Select(x => new SkillLevelDto(
                x.Id,
                x.TenantId,
                x.Order,
                x.Name,
                x.Description,
                x.PointsThreshold,
                x.IsActive
            ))
            .ToList();
}

public sealed class TenantSkillLevelSystemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                TenantSkillLevelSystem.Endpoint,
                async (ISender sender) =>
                    (await sender.Send(new TenantSkillLevelSystem.GetTenantSkillLevelSystemQuery()))
                    .ToHttpResult()
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<IReadOnlyList<SkillLevelDto>>()
            .WithName("GetTenantSkillLevelSystem")
            .WithTags("SkillLevelSystem");

        app.MapPut(
                TenantSkillLevelSystem.Endpoint,
                async (
                    TenantSkillLevelSystem.SetTenantSkillLevelSystemCommand body,
                    ISender sender
                ) => (await sender.Send(body)).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<IReadOnlyList<SkillLevelDto>>()
            .WithName("SetTenantSkillLevelSystem")
            .WithTags("SkillLevelSystem");
    }
}
