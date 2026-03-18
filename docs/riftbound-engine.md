# Riftbound Engine Technical Documentation

This document describes the current Riftbound simulation engine architecture, runtime behavior, and extension workflow.

## Scope

The document covers:

- Core runtime engine (`CreateSession`, `GetLegalActions`, `ApplyAction`)
- Turn, priority, chain, cleanup, and combat flow
- Energy/Power payment model
- Card and battlefield effect architecture
- Deferred player decisions (`PendingChoice`)
- API/service integration and persistence
- How to add and test new card effects

## Core Components

| Component | Responsibility | Key File |
|---|---|---|
| Simulation engine | Pure gameplay state transitions and rules execution | `Application/Features/Riftbound/Simulation/Engine/RiftboundSimulationEngine.cs` |
| Engine contract | Public API used by services/tests | `Application/Features/Riftbound/Simulation/Engine/IRiftboundSimulationEngine.cs` |
| Runtime session model | Serialized simulation state (players/zones/combat/choices) | `Domain/Simulation/RiftboundGameSession.cs` |
| Simulation service | Loads decks, calls engine, persists snapshots/events | `Application/Features/Riftbound/Simulation/Services/RiftboundSimulationService.cs` |
| Effect template resolver | Resolves fallback templates from card data/text | `Application/Features/Riftbound/Simulation/Definitions/RiftboundEffectTemplateResolver.cs` |
| Named effect catalog | Maps card name identifiers to effect classes | `Application/Features/Riftbound/Simulation/Effects/RiftboundNamedCardEffectCatalog.cs` |
| Effect runtime facade | Safe engine operations exposed to effect classes | `Application/Features/Riftbound/Simulation/Effects/IRiftboundEffectRuntime.cs` |
| Definition registry | Ruleset version, support checks, definition fallback | `Application/Features/Riftbound/Simulation/Services/FileBackedRiftboundSimulationDefinitionRegistry.cs` |

## Data Model

The engine mutates one `GameSession` object:

- Turn data: `TurnNumber`, `TurnPlayerIndex`, `Phase`, `State`
- Players: deck/hand/base/trash/champion/legend zones + rune pool
- Battlefields: units, gear, hidden cards, control/contest flags
- Stack-like flow: `Chain` entries for plays/moves
- Rule telemetry: `EffectContexts`
- Deferred UI decisions: `PendingChoice`

Important enums:

- `RiftboundTurnPhase`: `Setup`, `Awaken`, `Beginning`, `Channel`, `Draw`, `Action`, `End`, `Completed`
- `RiftboundTurnState`: `NeutralOpen`, `NeutralClosed`, `ShowdownOpen`, `ShowdownClosed`

## Engine Lifecycle

### 1) Session creation

`CreateSession(...)` builds both player states, shuffles/creates battlefields, draws opening hand (4), applies first-turn bonus for player 2, and immediately starts turn flow.

### 2) Legal action generation

`GetLegalActions(session)`:

- Returns no actions outside `Action` phase
- If `PendingChoice` exists, returns only `choose-*` options
- Otherwise generates:
  - rune/ability activations
  - playable card actions
  - movement actions
  - `end-turn` or `pass-focus` depending on priority window
- Expands contextual choices directly into action IDs (for example discard/return unit conquer choices)
- Filters unpayable play actions

### 3) Action execution

`ApplyAction(session, actionId)` normalizes legacy IDs to `v2:` format, validates legality, then dispatches to:

- `end-turn`
- `pass-focus`
- `choose-*` (pending choice resolver)
- resource activation
- play card
- move unit

## Action ID Format

All new actions use `v2:` prefix.

Typical forms:

- `v2:activate-rune-{guid}`
- `v2:activate-ability-{guid}`
- `v2:play-{guid}-to-base`
- `v2:play-{guid}-to-bf-{index}`
- `v2:play-{guid}-spell-target-unit-{guid}`
- `v2:play-{guid}-spell-target-units-{guid},{guid}[,...]`
- `v2:move-{guid}-to-base`
- `v2:move-{guid}-to-bf-{index}`
- `v2:choose-*` (deferred decision resolution)

Additional choices are encoded as markers inside action IDs, for example:

- Zaun Warrens discard choice: `-zaun-warrens-discard-{guid}`
- Emperor's Dais return-unit choice: `-emperors-dais-return-{guid}`

This allows deterministic replay because player choices are fully embedded in action history.

## Cost Model: Energy and Power

The engine uses a combined cost object:

- Energy: paid from `RunePool.Energy` (runes can auto-tap to fill missing energy)
- Power: paid by:
  - existing pooled power (`RunePool.PowerByDomain`)
  - recycling runes from base when required

Key rules implemented:

- Card base energy = `card.Cost`
- Card base power = `card.Power` with domain constraints from `card.ColorDomains`
- Hidden cards can pay flexible power (any rune domain)
- Repeat/accelerate/additional costs are merged into one total resource cost
- Costs can be dynamically reduced by aura/effect data (for example spell energy discounts)
- Power allocation uses a token planner so mixed/flexible requirements are paid correctly

## Priority, Chain, and Cleanup

Play and move actions open/advance the priority window:

- Acting player closes neutral state and passes priority to opponent
- `pass-focus` transitions between priority holder and focus holder
- Once both pass, pending chain effects resolve and cleanup runs

Cleanup responsibilities:

- Apply one-time damage prevention flags
- Kill dead units, run death triggers, detach gear, move dead cards
- Resolve contested battlefields into combat or conquer/score states
- Check victory score and complete simulation when needed

## Combat and Attacker/Defender Roles

During combat resolution:

- Engine establishes attacker and defender player indexes
- Units receive temporary `Attacker`/`Defender` designations (keywords) for the showdown
- Showdown start triggers run for units, attached gear, and legends
- Battlefield showdown triggers are executed (for example Reaver's Row)
- Combat contribution uses `EffectiveMight` plus temporary modifiers and assault/shield logic

After combat:

- Winners/losers are resolved
- Win-combat and conquer triggers execute
- Scores are applied
- Attacker/defender designations are cleared

## Effect Architecture

### Resolution layers

1. **Named card effect class** (preferred): implemented in `Simulation/Effects/Cards/...`
2. **Template fallback** from `RiftboundEffectTemplateResolver` (generic cards)
3. `unsupported` if neither path supports the card

### Named effect interface

`IRiftboundNamedCardEffect` exposes hooks for:

- legal action generation
- play triggers (`OnSpellOrGearPlay`, `OnUnitPlay`)
- movement/combat/hold/end-turn hooks
- keyword/aura queries
- activated ability execution
- battlefield scoring and modifiers
- discard and gear attach triggers

`RiftboundNamedCardEffectBase` provides no-op defaults.

### Battlefield effects are card effects

Battlefields are resolved through name identifier lookup and executed through the same effect interface.
This removes static battlefield logic from the engine and keeps behavior in dedicated effect classes.

## Deferred Decisions (`PendingChoice`)

Some effects must pause execution and wait for player input.

Flow:

1. Effect sets `session.PendingChoice` with:
   - `Kind`
   - owning `PlayerIndex`
   - source card metadata
   - list of `Options` with concrete `ActionId`s
2. `GetLegalActions` returns only those options
3. Player submits `v2:choose-*`
4. Engine routes by `PendingChoice.Kind` to the specific effect resolver
5. Resolution may continue chain/combat flow

Examples:

- Top-deck look and pick: Stacked Deck, Called Shot
- Vision keep/recycle choice: Forecaster
- Defensive movement choice at showdown start: Reaver's Row

## Targeting and Choice Semantics

Target-sensitive behavior is encoded so the acting player controls choices at the time they are needed:

- Spell targets are explicit in action IDs (`target-unit`, `target-units`)
- Same-location targeting is validated (for effects such as Bellows Breath)
- Conquer-related choices (discard/return unit) are action-bound markers, not precomputed hidden decisions
- Deferred choices are only surfaced when the game state requires them

This design supports deterministic simulations and reliable test replay.

## API and Persistence Integration

The simulation service wraps the engine with persistence and API DTOs:

- Create: `POST api/riftbound/simulations`
- Get: `GET api/riftbound/simulations/{simulationId}`
- Apply action: `POST api/riftbound/simulations/{simulationId}/actions`
- Autoplay: `POST api/riftbound/simulations/{simulationId}/autoplay`
- Deck readiness: `GET api/riftbound/decks/{deckId}/simulation-support`
- Deck batch tests: `POST api/riftbound/deck-tests`

Each action updates:

- serialized session snapshot (`SnapshotJson`)
- score summary
- append-only simulation event stream (`RiftboundSimulationEvent`)

## Adding a New Card Effect

Recommended workflow:

1. Create a dedicated effect class in:
   - `Application/Features/Riftbound/Simulation/Effects/Cards/Spells`
   - `.../Units`
   - `.../Gears`
   - `.../Battlefields`
2. Inherit `RiftboundNamedCardEffectBase`
3. Implement required hooks (`TryAddLegalActions`, `OnSpellOrGearPlay`, `OnUnitPlay`, etc.)
4. Use `IRiftboundEffectRuntime` for payments, draw, discard, reveal play, and context logging
5. Register effect in `RiftboundNamedCardEffectCatalog`
6. Ensure name identifier matches `RiftboundCardNameIdentifier.FromName(card.Name)`
7. Add focused behavior tests (one class per card or effect group)

## Testing Strategy

Current tests are organized as behavior suites under:

- `Tests/Unit.Tests/Features/Riftbound/Simulation`

Guidelines:

- Add one dedicated test class per new card/effect (or tight card group)
- Verify legal action generation and final state transitions
- Verify payment semantics (energy + power + optional/repeat/additional costs)
- Verify target/choice behavior with explicit action IDs
- Use deterministic setups via `RiftboundBehaviorTestFactory` and `RiftboundSimulationTestData`

## Related Docs

- Embedded model integration: `docs/riftbound-custom-model.md`
