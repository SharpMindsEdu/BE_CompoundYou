using Application.Shared.Services;

namespace Infrastructure.Services;

public class MockTeamSkillRequirementProvider : ITeamSkillRequirementProvider
{
    public Task<IReadOnlyList<TeamSkillRequirementStub>> GetRequirementsForTeamAsync(long teamId, CancellationToken ct = default)
    {
        // Stub: Return an empty list for now. 
        // In Phase 3, this will be replaced by real DB lookups.
        IReadOnlyList<TeamSkillRequirementStub> requirements = new List<TeamSkillRequirementStub>();
        return Task.FromResult(requirements);
    }
}
