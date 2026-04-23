# Automated Trading Foundation (Alpaca + OpenAI Agents)

This feature adds infrastructure and orchestration primitives for automated trading without exposing HTTP endpoints yet.

## What is included

- `ITradingDataProvider` abstraction in the domain layer.
- `AlpacaTradingDataProvider` infrastructure adapter for:
  - account/balance snapshot
  - open positions
  - latest quote per symbol
  - recent minute bars per symbol
- `AlpacaStreamingBackgroundService`:
  - consumes Alpaca `trade_updates` stream for order lifecycle cache
  - consumes Alpaca market-data stream for live quotes/bars cache
  - enables stream-first reads with REST fallback to reduce request volume
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
- OpenAI runtime can attach an MCP tool server for Alpaca agent calls
  (`https://alpaca.aboat-entertainment.com/mcp`).
- `TradingBacktestService` + endpoint `POST /api/trading/backtest` to simulate the
  same strategy over a historical date range.
- `TradingAutomationBackgroundService` that:
  1. runs daily watchlist sentiment analysis at 9:00 AM ET
  2. starts monitoring opportunities from 9:30 AM ET while market is open
  3. detects first-5-minute range breakout + retest
  4. asks OpenAI to validate retest strength
  5. uses fixed quantity from config (`OrderQuantity`, default 10)
  6. can execute leverage via long options (`UseOptionsTrading=true`)
  7. picks an option contract from Alpaca contracts API (direction-aware call/put)
  8. exits option position automatically when underlying hits configured TP/SL
     (market close order, then persisted fill update)
  9. falls back to stock bracket orders when `UseOptionsTrading=false`
  10. persists runtime state to disk to survive service restarts
  11. avoids duplicate entries when open positions/orders already exist
  12. persists live trades in Postgres immediately after order submission and
      updates them when Alpaca reports entry/exit fills (including actual fill prices)
- Alpaca provider extensions:
  - watchlist symbol retrieval (`/v2/watchlists/{id}`)
  - market clock (`/v2/clock`)
  - minute bars by time window
  - bracket order submission (`/v2/orders`)
  - option contracts lookup (`/v2/options/contracts`)
  - option quotes (`/v1beta1/options/quotes/latest`)
  - option order submission (`/v2/orders` with option symbols)
  - optional websocket streaming cache for quotes/bars/order updates

## Replaceability

All key components are behind interfaces:

- Replace Alpaca by implementing `ITradingDataProvider`.
- Replace OpenAI by implementing `ITradingAgentRuntime`.
- Define specialized agents by implementing `ITradingAgent`.
- Replace orchestration semantics by implementing `ITradingAgentOrchestrator`.

No controllers/endpoints were added.

## Live Trade Persistence

- Trades are stored in Postgres table `public.trading_trades`.
- A row is written as soon as the parent entry order is submitted.
- Rows are updated when:
  - entry fill is confirmed (`filled_avg_price` is stored as actual entry)
  - stop-loss / take-profit leg fills (actual exit + exit reason)
- Alpaca reconciliation fields are stored for matching history:
  - parent order id
  - bracket leg ids (TP/SL/exit)
  - Alpaca status fields
  - raw Alpaca order snapshots (payload JSON text)

## Configuration

Add these sections in `appsettings*.json`:

- `AlpacaTrading`
- `OpenAiTrading`
- `TradingAutomation`

### AlpacaTrading streaming fields

- `UseStreamingApi`: enables background websocket stream consumer.
- `UseTradingStream`: enables `trade_updates` websocket.
- `UseMarketDataStream`: enables market-data websocket (quotes/bars).
- `TradingStreamUrl`: e.g. `wss://paper-api.alpaca.markets/stream`.
- `MarketDataStreamUrl`: e.g. `wss://stream.data.alpaca.markets/v2/iex`.
- `StreamingReconnectDelaySeconds`: reconnect backoff.
- `StreamingMaxBarsPerSymbol`: in-memory bar buffer limit per symbol.
- `OptionDataFeed`: options market data feed for quote/snapshot endpoints
  (`indicative` recommended default unless you have OPRA entitlement).

### OpenAiTrading MCP fields

- `UseAlpacaMcpServer`: enables MCP tool integration for all OpenAI trading agent calls.
- `AlpacaMcpServerUrl`: remote MCP endpoint (`https://alpaca.aboat-entertainment.com/mcp`).
- `AlpacaMcpServerLabel`: tool server label exposed to the model.
- `AlpacaMcpAuthorization`: optional auth token forwarded as MCP tool `authorization`.
- `AlpacaMcpRequireApproval`: defaults to `never`.

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
- `UseOptionsTrading`: toggles options execution path (long calls/puts) when true.
- `OptionMinDaysToExpiration` / `OptionMaxDaysToExpiration`: target DTE window for
  contract selection.
- `OrderQuantity`: fixed quantity per trade used for live and backtest entries
  (10 means 10 contracts in options mode, default 10).
- `UseWholeShareQuantity`: rounds to full-share sizing when true.
- `StateFilePath`: persisted runtime state file path.
- `BacktestStartingEquity`: initial equity baseline for backtest performance tracking.
- `BacktestEstimatedSpreadBps`: spread model in basis points for backtest fills.
- `BacktestEstimatedSlippageBps`: slippage model in basis points for backtest fills.
- `BacktestCommissionPerUnit`: round-trip fee model per unit.
- `SentimentSystemPrompt` / `RetestValidationSystemPrompt`: OpenAI behavior control.

## Backtest endpoint

`POST /api/trading/backtest`

Request body example:

```json
{
  "startDate": "2026-04-01",
  "endDate": "2026-04-15",
  "watchlistId": "a5e81fdf-683a-4fc0-ae4a-e0ef2cea8e2e",
  "maxOpportunities": 3,
  "minimumSentimentScore": 70,
  "minimumRetestScore": 70,
  "useAiSentiment": true,
  "useAiRetestValidation": true
}
```

Response includes aggregate stats and per-trade details:

- total trades, wins/losses, win rate
- total and average PnL
- per-day summary
- per-trade entry/SL/TP/exit with R-multiple

The API keys are intentionally empty and expected from environment- or secret-based configuration.

## Monitoring endpoints

### Trading trades

- `GET /api/trading/trades`
  - paginated + filterable list
  - filters: `symbol`, `status`, `direction`, `exitReason`, `submittedFromUtc`,
    `submittedToUtc`, `minRealizedProfitLoss`, `maxRealizedProfitLoss`
  - pagination: `page`, `pageSize`
  - sorting: `sortAscending` (by `submittedAtUtc`)
- `GET /api/trading/trades/{id}`
  - full trade details including Alpaca payload snapshots
- `GET /api/trading/trades/summary`
  - aggregate metrics for the filtered set
  - includes totals, win/loss/breakeven counts, realized PnL, average R, win rate

### Exception logs

- `GET /api/diagnostics/exceptions`
  - paginated + filterable list
  - filters: `search`, `exceptionType`, `captureKind`, `isHandled`, `requestPath`,
    `requestMethod`, `occurredFromUtc`, `occurredToUtc`
  - pagination: `page`, `pageSize`
  - sorting: `sortAscending` (by `occurredOnUtc`)
- `GET /api/diagnostics/exceptions/{id}`
  - full exception details including stack trace and metadata JSON
- `GET /api/diagnostics/exceptions/summary`
  - aggregate metrics for the filtered set
  - includes handled/unhandled counts and top exception types
