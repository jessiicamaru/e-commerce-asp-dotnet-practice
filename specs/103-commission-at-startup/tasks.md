---
description: "Task list for A missing commission rate stops Order at startup"
---

# Tasks: A missing commission rate stops Order at startup

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: written first and seen red (the check does not exist yet, then it lacks the rate).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: The check (US1)

- [X] T001 [US1] `RequiredSettingsTests` first: passes with a full configuration; throws naming the setting without the rate, with `1.5`, and without shipping options
- [X] T002 [US1] `RequiredSettings.Check` resolving `ConfiguredShippingOptions`, `ITaxRates` and `ICommissionRate`; `Program.cs` calls it

## Phase 2: Verification and docs

- [X] T003 Mutation (quickstart Scenario 2) red - it is the code before the fix: 5 of 7 failed; dropping the shipping check is red too; Order 293/293
- [X] T004 `Marketplace__CommissionRate=` makes the real process exit with the setting's name; rebuilt Order container healthy; `verify-saga.sh` passes; post-design Constitution re-check: no violations
- [X] T005 Docs: `docs/features/marketplace.md` known limit removed, CLAUDE.md, backlog, timeline
- [ ] T006 Merged as #223, closing #210
