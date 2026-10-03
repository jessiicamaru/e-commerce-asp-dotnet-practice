---
description: "Task list for Deploy and roll back from CI"
---

# Tasks: Deploy and roll back from CI

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: On the server (US1, US2)

- [ ] T001 `server/deploy/deploy.sh`:
  - name check, env check and registry check (refuse with exit 2);
  - lock, pull, `up --wait`, smoke checks;
  - release log;
  - `previous`;
  - automatic rollback;
  - `--dry-run`.
- [ ] T002 `server/deploy/test-deploy.sh`: stub `docker` and `curl`; every branch of the state diagram
- [ ] T003 Mutations: the rollback never runs; `previous` picks the current release; the name check removed

## Phase 2: In CI (US3)

- [ ] T004 `.github/scripts/verify-production-overlay.sh`: shipped files only, filled env template, rendered-config
  assertions, then `deploy.sh --dry-run`
- [ ] T005 CI job `deploy-dry-run`; a negative control (a database port published) fails it
- [ ] T006 `.github/workflows/deploy.yml`:
  - resolve the release, ship that commit's files;
  - SSH with a pinned host key;
  - missing secrets named;
  - dry run;
  - a summary.

## Phase 3: For real (SC-001, SC-002)

- [ ] T007 On this machine with published images: deploy A, deploy B, `previous` → A
- [ ] T008 A deploy forced to fail rolls back to the previous release

## Phase 4: Docs

- [ ] T009 `docs/infrastructure/production.md` §8, CLAUDE.md, timeline, backlog
- [ ] T010 Merged, closes #288

## Evidence

(Filled in when the work is verified.)
