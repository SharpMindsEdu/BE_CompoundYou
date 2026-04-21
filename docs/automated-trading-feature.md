# Automated Trading Foundation (Alpaca + OpenAI Agents)

This feature adds infrastructure and orchestration primitives for automated trading without exposing HTTP endpoints yet.

## What is included

- `ITradingDataProvider` abstraction in the domain layer.
- `AlpacaTradingDataProvider` infrastructure adapter for:
  - account/balance snapshot
  - open positions
  - latest quote per symbol
  - recent minute bars per symbol
- Agent orchestration contracts:
  - `ITradingAgent`
  - `ITradingAgentRuntime`
  - `ITradingAgentOrchestrator`
- `TradingAgentOrchestrator` that:
  1. builds a market snapshot from the provider
  2. executes all registered agents in sequence
  3. stores agent outputs in shared context memory for downstream agents
- `OpenAiTradingAgentRuntime` that calls OpenAI Responses API (`/v1/responses`).
- `OpenAiTradingAgent` generic implementation, configurable through appsettings.

## Replaceability

All key components are behind interfaces:

- Replace Alpaca by implementing `ITradingDataProvider`.
- Replace OpenAI by implementing `ITradingAgentRuntime`.
- Define specialized agents by implementing `ITradingAgent`.
- Replace orchestration semantics by implementing `ITradingAgentOrchestrator`.

No controllers/endpoints were added.

## Configuration

Add these sections in `appsettings*.json`:

- `AlpacaTrading`
- `OpenAiTrading`
- `TradingAutomation`

The API keys are intentionally empty and expected from environment- or secret-based configuration.
