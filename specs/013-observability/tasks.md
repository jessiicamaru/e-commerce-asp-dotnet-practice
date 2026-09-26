# Tasks: Following One Order Across Seven Services

> Completed on 2026-09-27, after the feature merged (#33), from the code at that merge, the pull
> request and docs/guides/observability.md (this feature has no page under docs/features/).

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md) | **Branch**: `013-observability`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: No automated test tasks. The claims were verified by hand against the running containers
(T009), as the plan's Complexity Tracking records.

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 (one query, one trace), US2 (the stall is visible), US3 (nothing secret)

The first eleven tasks are the list written at the merge, kept as they were; story labels were not
part of it.

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

## Recorded after the merge

Work inside the pull request that the list above did not name, added on 2026-09-27 from the diff of #33.

- [X] T012 [US1] Add the four OpenTelemetry packages (1.19.x) to `server/src/BuildingBlocks/Ecommerce.Shared/Ecommerce.Shared.csproj`, and a project reference to `Ecommerce.Shared` in `server/src/ApiGateway/Ecommerce.ApiGateway/Ecommerce.ApiGateway.csproj` - the gateway had none before
- [X] T013 [US1] Exclude `/health` from the ASP.NET Core instrumentation in `ObservabilityExtensions.cs`, so Docker's probes do not bury the checkouts
- [X] T014 [US1] Tag the checkout's request span `order.id` with `Activity.Current?.SetTag` after the one save in `server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/SubmitOrder/SubmitOrderCommandHandler.cs`
- [X] T015 [US3] Register the propagator as a singleton `DistributedContextPropagator` in `server/src/ApiGateway/Ecommerce.ApiGateway/Program.cs`, before `AddObservability("gateway")`
- [X] T016 [P] [US1] Document the queries that matter in `docs/guides/observability.md` (by order, by trace, saga steps, warnings and errors, the no-instance Warning)
- [X] T011 PR [#33](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/33) `Closes #22`; CI green; squash-merged as `7301943` on 2026-09-22

## Dependencies & Execution Order

- **T001-T003** first: the shared pieces every service calls. T012 is part of T001.
- **T004** needs T001-T003; **T005, T006, T014** are independent of each other and of T004's wiring.
- **T007** (Seq) is needed only for **T009**, which needs everything else deployed in containers.

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

## Notes

- **T011 is listed last although its id is lower**: it was the last task of the record written at the
  merge, and the tasks added afterwards describe work already inside that pull request.
- 16 tasks, all done. None is an automated test; T009 is the verification.
