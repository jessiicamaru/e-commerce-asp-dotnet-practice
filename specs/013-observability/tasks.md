# Tasks: Following One Order Across Seven Services

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md) | **Branch**: `013-observability`

- [X] T001 `Ecommerce.Shared/Observability/ObservabilityExtensions.cs` — logs + traces over OTLP, optional on `OTLP_ENDPOINT`
- [X] T002 `OrderIdLogScopeFilter<T>` — `OrderId` scope + `order.id` span tag for every consumed message about an order
- [X] T003 `IgnoreIncomingTraceContextPropagator` — the gateway mints every trace
- [X] T004 Wire all seven services and the gateway; the filter into six buses
- [X] T005 Saga: transitions at Information; missing instance at Warning (`OnMissingInstance`)
- [X] T006 Checkout logs the order id it creates
- [X] T007 Seq in `docker-compose.yml` (admin password required from `.env`); `OTLP_ENDPOINT` in the overlay; `.env.example`
- [X] T008 EF command logging to Warning in every service; Orchestrator gets an `appsettings.json`
- [X] T009 Verify: one `OrderId` query, one trace across services, a Warning for an orphaned reply, no JWT in any event
- [X] T010 Docs: guide, CLAUDE.md, roadmap Phase 7, README index
- [ ] T011 PR `Closes #22`; CI green; squash-merge

## What actually happened

```text
OrderId = '<order>'          8 lines: order 2, orchestrator 3, inventory 2, payment 1
                             (151 before EF command logging was lowered to Warning)
trace behind those lines     1 trace id, spanning cart, catalog, identity, inventory,
                             orchestrator, order, payment
orphaned InventoryReserved   WARN  Saga <id>: InventoryReservedEvent arrived but no saga
                             instance exists for it, so it was discarded ...
JWT-looking strings          0
dotnet test                  113/113; verify-saga and verify-auth pass with telemetry on
```

Reverting the orchestrator's outbox did **not** reproduce the stall on demand — the race is
timing-dependent — so the mechanism (a reply with no instance) was exercised directly (research D7).
