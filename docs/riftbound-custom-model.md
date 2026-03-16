# Riftbound Embedded Model Integration

Riftbound uses an embedded C# model service inside the backend (no external LLM call).

## What it learns

- Action decisions in gameplay
- Reaction decisions in chain/focus situations
- Deck construction preferences for optimization runs

## Runtime architecture

1. `EmbeddedRiftboundAiModelService` is registered as:
   - `IRiftboundAiModelService` (inference)
   - `IRiftboundAiOnlineTrainer` (online training)
   - `IRiftboundTrainingDataStore` (JSONL sample output)
2. `RiftboundModelMovePolicy` (`policyId = "riftbound-model"`) queries the embedded model.
3. `LlmMovePolicy` (`policyId = "llm"`) remains as a compatibility alias and delegates to `riftbound-model`.
4. `RiftboundSimulationService.AutoPlayAsync` feeds action traces + winner into online training.
5. `RiftboundDeckOptimizationService` feeds self-play action traces and deck outcomes into online training.

## Training source

The model is trained from random/self-play test runs via:

- simulation autoplay traces
- deck optimization matchup simulations

Rewards are winner-based:

- winner decisions/decks: positive reward
- loser decisions/decks: negative reward
- draws: neutral reward

## Configuration

`Api/appsettings*.json`:

```json
"RiftboundAiModel": {
  "Enabled": true,
  "TrainingEnabled": true,
  "ExplorationRate": 0.12,
  "PersistModelToDisk": true,
  "AutosaveIntervalSeconds": 30,
  "ModelFilePath": "data/riftbound-training/embedded-model.json",
  "CaptureTrainingData": true,
  "TrainingDataDirectory": "data/riftbound-training",
  "MinSamplesForDeckBuild": 50
}
```

## Outputs

- model snapshot: `data/riftbound-training/embedded-model.json`
- action samples: `data/riftbound-training/action-decisions.jsonl`
- deck build samples: `data/riftbound-training/deck-builds.jsonl`
