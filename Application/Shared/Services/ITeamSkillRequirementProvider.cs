namespace Application.Shared.Services;

public record TeamSkillRequirementStub(long SkillId, long RequiredSkillLevelId, int Weight = 1);

/// <summary>
/// Retrieves configured team-level skill requirements for gap analysis and
/// career readiness calculations.
/// </summary>
public interface ITeamSkillRequirementProvider
{
    Task<IReadOnlyList<TeamSkillRequirementStub>> GetRequirementsForTeamAsync(long teamId, CancellationToken ct = default);
}
