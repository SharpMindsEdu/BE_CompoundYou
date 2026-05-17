namespace Application.Shared.Services;

public record TeamSkillRequirementStub(long SkillId, long RequiredSkillLevelId, int Weight = 1);

/// <summary>
/// Mock interface for retrieving team skill requirements.
/// This will be replaced by a real implementation in Phase 3.
/// </summary>
public interface ITeamSkillRequirementProvider
{
    Task<IReadOnlyList<TeamSkillRequirementStub>> GetRequirementsForTeamAsync(long teamId, CancellationToken ct = default);
}
