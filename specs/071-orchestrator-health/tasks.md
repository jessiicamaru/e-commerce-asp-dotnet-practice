---
description: "Task list for The Orchestrator answers /health"
---

# Tasks: The Orchestrator answers /health

> Completed on 2026-09-27, after the feature merged (#155), from the code at that merge, the pull request and
> docs/architecture/microservices-design.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/http-api.md](contracts/http-api.md)

## Format: `[ID] [P?] [Story] Description`

## As first recorded

- [X] T001 Add the health checks and `/health` in `server/src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/Program.cs`.
- [X] T002 Gateway cluster and route (`appsettings.json`), and the compose override. Re-enable the compose health check.
- [X] T003 The CI saga job waits for it. `verify-saga.sh` reports its health on a stall.
- [X] T004 A test, the live check up and down through the gateway, and docs: CLAUDE.md (the recorded disagreement is resolved) and the architecture pages.

## Detail added in the backfill (all done in #155)

- [X] T005 [US1] Add `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` to `server/src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/Ecommerce.Orchestrator.WebApi.csproj`
- [X] T006 [US1] `server/tests/Ecommerce.Orchestrator.Tests/HealthTests.cs` (2) with `Microsoft.AspNetCore.Mvc.Testing` in the test `.csproj`; mutation: dropping the database check fails both
- [X] T007 [US2] `server/docker-compose.app.yml`: the Orchestrator back on `service-healthcheck`; the gateway `depends_on` it healthy; the `orchestrator-cluster` address override
- [X] T008 [US2] `.github/workflows/ci.yml`: `orchestrator:5058` in both readiness loops of the saga job
- [X] T009 [US2] `.github/scripts/verify-saga.sh`: `ORCHESTRATOR_URL`, probed with the rest; the stall message reports its `/health` code
- [X] T010 Live: 200 up, 502 stopped, 200 restarted and docker `healthy`; `verify-saga.sh` `approve=pass`
- [X] T011 [P] Docs: CLAUDE.md gotcha, `microservices-design.md`, `running-in-containers.md`, checkout's known limits, `docs/reference/gateway.md`, counts, timeline, backlog
- [X] T012 Merged as #155 on 2026-09-26 (`f7b609f`), closing #115

## Dependencies

T001 and T005 before T006; T002 and T007 before the live check; T003, T008, T009 after `/health` exists.
