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
- `OpenAiTradingSignalAgent` specialized for:
  - 9:00 AM ET watchlist sentiment scan (up to 3 opportunities)
  - breakout retest validation scoring for intraday entries
- `TradingAutomationBackgroundService` that:
  1. runs daily watchlist sentiment analysis at 9:00 AM ET
  2. starts monitoring opportunities from 9:30 AM ET while market is open
  3. detects first-5-minute range breakout + retest
  4. asks OpenAI to validate retest strength
  5. places bracket orders (SL near retest, TP >= 2R)
- Alpaca provider extensions:
  - watchlist symbol retrieval (`/v2/watchlists/{id}`)
  - market clock (`/v2/clock`)
  - minute bars by time window
  - bracket order submission (`/v2/orders`)

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

### TradingAutomation key fields

- `Enabled`: toggles background worker.
- `TimeZoneId`: use `Eastern Standard Time` for ET scheduling on Windows.
- `WatchlistId`: Alpaca watchlist id (configured to `a5e81fdf-683a-4fc0-ae4a-e0ef2cea8e2e`).
- `SentimentScanHour` / `SentimentScanMinute`: defaults 9:00 ET.
- `MarketOpenHour` / `MarketOpenMinute`: defaults 9:30 ET.
- `MaxOpportunities`: max symbols selected from sentiment scan (defaults to 3).
- `MinimumSentimentScore` / `MinimumRetestScore`: thresholds for progression.
- `StopLossBufferPercent`: SL buffer around retest extremes.
- `RewardToRiskRatio`: reward target multiplier (2.0 means minimum 2R).
- `OrderQuantity`: quantity used for bracket orders.
- `SentimentSystemPrompt` / `RetestValidationSystemPrompt`: OpenAI behavior control.

The API keys are intentionally empty and expected from environment- or secret-based configuration.
