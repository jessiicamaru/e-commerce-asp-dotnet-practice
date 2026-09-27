---
description: "Task list for A missing commission rate stops Order at startup"
---

# Tasks: A missing commission rate stops Order at startup

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: written first and seen red (the check does not exist yet, then it lacks the rate).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: The check (US1)

- [ ] T001 [US1] `RequiredSettingsTests` first: passes with a full configuration; throws naming the setting without the rate, with `1.5`, and without shipping options
- [ ] T002 [US1] `RequiredSettings.Check` resolving `ConfiguredShippingOptions`, `ITaxRates` and `ICommissionRate`; `Program.cs` calls it

## Phase 2: Verification and docs

- [ ] T003 Mutation (quickstart Scenario 2), red; full Order suite
- [ ] T004 Rebuilt Order container healthy; `verify-saga.sh` passes; post-design Constitution re-check
- [ ] T005 Docs: `docs/features/marketplace.md` known limit removed, CLAUDE.md, backlog, timeline
- [ ] T006 Merged as #223, closing #210
