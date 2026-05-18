using Application.Shared.Services;
using Domain.Entities;
using Domain.Repositories;

namespace Infrastructure.Services;

public sealed class TeamSkillRequirementProvider(IRepository<TeamSkillRequirement> requirements)
    : ITeamSkillRequirementProvider
{
    public async Task<IReadOnlyList<TeamSkillRequirementStub>> GetRequirementsForTeamAsync(
        long teamId,
        CancellationToken ct = default
    )
    {
        var rows = await requirements.ListAll(x => x.TeamId == teamId, ct);
        return rows
            .OrderBy(x => x.SkillId)
            .Select(x => new TeamSkillRequirementStub(
                x.SkillId,
                x.RequiredSkillLevelId,
                x.Weight <= 0 ? 1 : x.Weight
            ))
            .ToList();
    }
}
