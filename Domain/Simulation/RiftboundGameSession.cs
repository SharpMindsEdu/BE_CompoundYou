namespace Domain.Simulation;

public sealed record RulesetVersion(string Value);

public enum RiftboundTurnPhase
{
    Setup,
    Awaken,
    Beginning,
    Channel,
    Draw,
    Action,
    End,
    Completed,
}

public enum RiftboundTurnState
{
    NeutralOpen,
    NeutralClosed,
    ShowdownOpen,
    ShowdownClosed,
}

public enum RiftboundActionType
{
    EndTurn,
    ActivateRune,
    PlayCard,
    StandardMove,
    ResolveCombat,
    PassFocus,
}

public sealed class GameSession
{
    public required long SimulationId { get; init; }
    public required RulesetVersion RulesetVersion { get; init; }
    public required int TurnPlayerIndex { get; set; }
    public required int TurnNumber { get; set; }
    public required RiftboundTurnPhase Phase { get; set; }
    public required RiftboundTurnState State { get; set; }
    public required List<PlayerState> Players { get; init; }
    public required List<BattlefieldState> Battlefields { get; init; }
    public required ShowdownState Showdown { get; init; }
    public required CombatState Combat { get; init; }
    public required List<ChainItem> Chain { get; init; }
    public required List<EffectContext> EffectContexts { get; init; }
    public required Dictionary<string, bool> UsedScoringKeys { get; init; }
}

public sealed class PlayerState
{
    public required int PlayerIndex { get; init; }
    public required long UserId { get; init; }
    public required long DeckId { get; init; }
    public required string Policy { get; set; }
    public required int Score { get; set; }
    public required bool FirstTurnExtraChannelBonus { get; set; }
    public required ZoneState MainDeckZone { get; init; }
    public required ZoneState RuneDeckZone { get; init; }
    public required ZoneState HandZone { get; init; }
    public required ZoneState BaseZone { get; init; }
    public required ZoneState TrashZone { get; init; }
    public required ZoneState ChampionZone { get; init; }
    public required ZoneState LegendZone { get; init; }
    public required RunePool RunePool { get; init; }
}

public sealed class ZoneState
{
    public required string Name { get; init; }
    public required List<CardInstance> Cards { get; init; }
}

public sealed class RunePool
{
    public int Energy { get; set; }
    public Dictionary<string, int> PowerByDomain { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BattlefieldState
{
    public required long CardId { get; init; }
    public required string Name { get; init; }
    public required int Index { get; init; }
    public int? ControlledByPlayerIndex { get; set; }
    public int? ContestedByPlayerIndex { get; set; }
    public bool IsShowdownStaged { get; set; }
    public bool IsCombatStaged { get; set; }
    public List<CardInstance> Units { get; set; } = [];
    public List<CardInstance> Gear { get; set; } = [];
    public List<CardInstance> HiddenCards { get; set; } = [];
}

public sealed class CardInstance
{
    public required Guid InstanceId { get; init; }
    public required long CardId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required int OwnerPlayerIndex { get; init; }
    public required int ControllerPlayerIndex { get; set; }
    public int? Cost { get; init; }
    public int? Might { get; set; }
    public int MarkedDamage { get; set; }
    public bool IsExhausted { get; set; }
    public bool IsFacedown { get; set; }
    public bool IsPending { get; set; }
    public bool IsFinalized { get; set; }
    public bool IsToken { get; set; }
    public bool IsHidden { get; set; }
    public int ShieldCount { get; set; }
    public int TemporaryMightModifier { get; set; }
    public int PermanentMightModifier { get; set; }
    public Guid? AttachedToInstanceId { get; set; }
    public string EffectTemplateId { get; set; } = "unsupported";
    public Dictionary<string, string> EffectData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Keywords { get; set; } = [];
}

public sealed class ChainItem
{
    public required string ActionId { get; init; }
    public required int ControllerPlayerIndex { get; init; }
    public required Guid CardInstanceId { get; init; }
    public required string Kind { get; init; }
    public bool IsPending { get; set; }
    public bool IsFinalized { get; set; }
}

public sealed class ShowdownState
{
    public bool IsOpen { get; set; }
    public int? BattlefieldIndex { get; set; }
    public int? FocusPlayerIndex { get; set; }
    public int? PriorityPlayerIndex { get; set; }
    public bool HasInitialChainResolved { get; set; }
}

public sealed class CombatState
{
    public bool IsOpen { get; set; }
    public int? BattlefieldIndex { get; set; }
    public int? AttackerPlayerIndex { get; set; }
    public int? DefenderPlayerIndex { get; set; }
}

public sealed class EffectContext
{
    public required string Source { get; init; }
    public required int ControllerPlayerIndex { get; init; }
    public required string Timing { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
