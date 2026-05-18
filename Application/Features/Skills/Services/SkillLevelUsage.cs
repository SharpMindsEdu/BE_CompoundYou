using Application.Shared;
using Domain.Entities;
using Domain.Repositories;

namespace Application.Features.Skills.Services;

internal static class SkillLevelUsage
{
    public static async Task<Result<SkillLevel>> GetUsableTenantLevelAsync(
        IRepository<SkillLevel> skillLevels,
        ICurrentTenant currentTenant,
        long levelId,
        CancellationToken ct
    )
    {
        if (!currentTenant.HasTenant)
            return Result<SkillLevel>.Failure(
                TenancyErrors.NoTenantInContext,
                ResultStatus.Forbidden
            );

        var level = await skillLevels.GetByExpression(
            l => l.Id == levelId
                 && l.SkillId == null
                 && l.TenantId == currentTenant.TenantId
                 && l.IsActive,
            ct
        );
        if (level is not null)
            return Result<SkillLevel>.Success(level);

        return Result<SkillLevel>.Failure(
            "Invalid tenant skill level. Configure the tenant level system in Skill Library.",
            ResultStatus.BadRequest
        );
    }
}
