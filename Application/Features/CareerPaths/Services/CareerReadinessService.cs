using Application.Features.Career.DTOs;
using Application.Shared;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;

namespace Application.Features.CareerPaths.Services;

internal sealed class CareerReadinessService(
    IRepository<EmployeeRoleProfile> employeeRoleProfiles,
    IRepository<RoleProfile> roleProfiles,
    IRepository<JobFamily> jobFamilies,
    IRepository<CareerLevel> careerLevels,
    IRepository<RoleProfileSkillRequirement> requirements,
    IRepository<EmployeeSkillAssessment> assessments,
    IRepository<Skill> skills,
    IRepository<SkillLevel> skillLevels)
    : ICareerReadinessService
{
    public async Task<CareerPathDto> CalculateAsync(
        long employeeId,
        long? targetRoleProfileId = null,
        CancellationToken ct = default
    )
    {
        var scoredOn = DateTimeOffset.UtcNow;
        var currentAssignment = await employeeRoleProfiles.GetByExpression(
            x => x.EmployeeId == employeeId && x.IsActive,
            ct
        );
        if (currentAssignment is null)
            return Empty(employeeId, scoredOn);

        var currentRole = await BuildRoleSummaryAsync(currentAssignment.RoleProfileId, ct);
        if (currentRole is null)
            return Empty(employeeId, scoredOn);

        var targetRole = targetRoleProfileId.HasValue
            ? await BuildRoleSummaryAsync(targetRoleProfileId.Value, ct)
            : await FindNextRoleAsync(currentRole, ct);

        if (targetRole is null)
            return new CareerPathDto(
                employeeId,
                currentRole,
                null,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<CareerSkillGapDto>(),
                scoredOn
            );

        var targetRequirements = await requirements.ListAll(
            x => x.RoleProfileId == targetRole.Id,
            ct
        );
        if (targetRequirements.Count == 0)
            return new CareerPathDto(
                employeeId,
                currentRole,
                targetRole,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<CareerSkillGapDto>(),
                scoredOn
            );

        var employeeAssessments = await assessments.ListAll(
            x => x.EmployeeId == employeeId && x.Status == SkillAssessmentStatus.Validated,
            ct
        );
        var requiredSkillIds = targetRequirements.Select(r => r.SkillId).Distinct().ToArray();
        var requiredLevelIds = targetRequirements.Select(r => r.RequiredSkillLevelId).Distinct().ToArray();
        var actualLevelIds = employeeAssessments
            .Where(a => a.ValidatedSkillLevelId.HasValue)
            .Select(a => a.ValidatedSkillLevelId!.Value)
            .Distinct()
            .ToArray();
        var allLevelIds = requiredLevelIds.Concat(actualLevelIds).Distinct().ToArray();
        var allSkills = await skills.ListAll(x => requiredSkillIds.Contains(x.Id), ct);
        var allLevels = await skillLevels.ListAll(x => allLevelIds.Contains(x.Id), ct);

        decimal totalWeight = 0;
        decimal weightedFit = 0;
        var validatedRequirementCount = 0;
        var gaps = new List<CareerSkillGapDto>();

        foreach (var requirement in targetRequirements)
        {
            var requiredLevel = allLevels.FirstOrDefault(x => x.Id == requirement.RequiredSkillLevelId);
            if (requiredLevel is null)
                continue;

            var assessment = employeeAssessments.FirstOrDefault(x => x.SkillId == requirement.SkillId);
            var actualLevel = assessment?.ValidatedSkillLevelId is null
                ? null
                : allLevels.FirstOrDefault(x => x.Id == assessment.ValidatedSkillLevelId.Value);

            var actualOrder = actualLevel?.Order ?? 0;
            var requiredOrder = Math.Max(requiredLevel.Order, 1);
            var weight = requirement.Weight <= 0 ? 1m : requirement.Weight;
            var fit = Math.Min(actualOrder / (decimal)requiredOrder, 1m);

            weightedFit += fit * weight;
            totalWeight += weight;

            if (assessment?.ValidatedSkillLevelId is not null)
                validatedRequirementCount++;

            var skill = allSkills.FirstOrDefault(x => x.Id == requirement.SkillId);
            gaps.Add(
                new CareerSkillGapDto(
                    requirement.SkillId,
                    skill?.Name ?? $"Skill #{requirement.SkillId}",
                    requiredLevel.Id,
                    requiredLevel.Name,
                    requiredLevel.Order,
                    actualLevel?.Id,
                    actualLevel?.Name,
                    actualOrder,
                    actualOrder - requiredLevel.Order,
                    weight,
                    actualOrder >= requiredLevel.Order
                )
            );
        }

        if (totalWeight == 0)
            return Empty(employeeId, scoredOn) with
            {
                CurrentRole = currentRole,
                TargetRole = targetRole,
            };

        var skillFitScore = (int)Math.Round(weightedFit / totalWeight * 100m, MidpointRounding.AwayFromZero);
        var validationCoverageScore = (int)Math.Round(
            validatedRequirementCount / (decimal)targetRequirements.Count * 100m,
            MidpointRounding.AwayFromZero
        );
        var readinessScore = (int)Math.Round(
            skillFitScore * 0.85m + validationCoverageScore * 0.15m,
            MidpointRounding.AwayFromZero
        );

        return new CareerPathDto(
            employeeId,
            currentRole,
            targetRole,
            readinessScore,
            skillFitScore,
            validationCoverageScore,
            null,
            BandFor(readinessScore),
            gaps.OrderBy(x => x.IsMet).ThenBy(x => x.Gap).ThenBy(x => x.SkillName).ToList(),
            scoredOn
        );
    }

    private async Task<CareerRoleSummaryDto?> FindNextRoleAsync(
        CareerRoleSummaryDto currentRole,
        CancellationToken ct
    )
    {
        var nextLevel = (await careerLevels.ListAll(
                x =>
                    x.JobFamilyId == currentRole.JobFamilyId
                    && x.Order > currentRole.CareerLevelOrder,
                ct
            ))
            .OrderBy(x => x.Order)
            .FirstOrDefault();
        if (nextLevel is null)
            return null;

        var nextRole = (await roleProfiles.ListAll(
                x =>
                    x.JobFamilyId == currentRole.JobFamilyId
                    && x.CareerLevelId == nextLevel.Id
                    && x.IsActive,
                ct
            ))
            .OrderBy(x => x.Name)
            .FirstOrDefault();

        return nextRole is null ? null : await BuildRoleSummaryAsync(nextRole.Id, ct);
    }

    private async Task<CareerRoleSummaryDto?> BuildRoleSummaryAsync(long roleProfileId, CancellationToken ct)
    {
        var role = await roleProfiles.GetById(roleProfileId);
        if (role is null || !role.IsActive)
            return null;

        var family = await jobFamilies.GetById(role.JobFamilyId);
        var level = await careerLevels.GetById(role.CareerLevelId);
        if (family is null || level is null)
            return null;

        return new CareerRoleSummaryDto(
            role.Id,
            role.Name,
            family.Id,
            family.Name,
            level.Id,
            level.Name,
            level.Order
        );
    }

    private static CareerReadinessBand BandFor(int score) =>
        score >= 85 ? CareerReadinessBand.Ready
        : score >= 60 ? CareerReadinessBand.Developing
        : CareerReadinessBand.AtRisk;

    private static CareerPathDto Empty(long employeeId, DateTimeOffset scoredOn) =>
        new(
            employeeId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Array.Empty<CareerSkillGapDto>(),
            scoredOn
        );
}
