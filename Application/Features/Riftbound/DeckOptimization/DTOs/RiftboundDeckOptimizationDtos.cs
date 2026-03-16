namespace Application.Features.Riftbound.DeckOptimization.DTOs;

public sealed record RiftboundDeckOptimizationRunDto(
    long RunId,
    string Status,
    int PopulationSize,
    int Generations,
    int SeedsPerMatch,
    int MaxAutoplaySteps,
    int CurrentGeneration,
    decimal ProgressPercent,
    long Seed,
    string? ErrorMessage,
    DateTimeOffset? StartedOn,
    DateTimeOffset? CompletedOn,
    int CandidateCount,
    int MatchupCount
);

public sealed record RiftboundDeckOptimizationLeaderboardEntryDto(
    int RankGlobal,
    int RankInLegend,
    int Generation,
    long DeckId,
    string DeckName,
    long LegendId,
    string LegendName,
    int Wins,
    int Losses,
    int Draws,
    int GamesPlayed,
    decimal WinRate,
    decimal SonnebornBerger,
    decimal HeadToHeadScore
);

public sealed record RiftboundDeckOptimizationLegendLeaderboardDto(
    long LegendId,
    string LegendName,
    IReadOnlyCollection<RiftboundDeckOptimizationLeaderboardEntryDto> Entries
);

public sealed record RiftboundDeckOptimizationLeaderboardDto(
    long RunId,
    string Status,
    int Generation,
    IReadOnlyCollection<RiftboundDeckOptimizationLeaderboardEntryDto> Global,
    IReadOnlyCollection<RiftboundDeckOptimizationLegendLeaderboardDto> ByLegend
);
