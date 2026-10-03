---
description: "Task list for Deploy and roll back from CI"
---

# Tasks: Deploy and roll back from CI

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: On the server (US1, US2)

- [x] T001 `server/deploy/deploy.sh`:
  - name check, env check and registry check (refuse with exit 2);
  - lock, pull, `up --wait`, smoke checks;
  - release log;
  - `previous`;
  - automatic rollback;
  - `--dry-run`.
- [x] T002 `server/deploy/test-deploy.sh`: stub `docker` and `curl`; every branch of the state diagram
- [x] T003 Mutations: the rollback never runs; `previous` picks the current release; the name check removed

## Phase 2: In CI (US3)

- [x] T004 `.github/scripts/verify-production-overlay.sh`: shipped files only, filled env template, rendered-config
  assertions, then `deploy.sh --dry-run`
- [x] T005 CI job `deploy-dry-run`; a negative control (a database port published) fails it
- [x] T006 `.github/workflows/deploy.yml`:
  - resolve the release, ship that commit's files;
  - SSH with a pinned host key;
  - missing secrets named;
  - dry run;
  - a summary.

## Phase 3: For real (SC-001, SC-002)

- [x] T007 On this machine with published images: deploy A, deploy B, `previous` → A
- [x] T008 A deploy forced to fail rolls back to the previous release

## Phase 4: Docs

- [x] T009 `docs/infrastructure/production.md` §8, CLAUDE.md, timeline, backlog
- [x] T010 Merged, closes #288 - #297

## Evidence

- `server/deploy/test-deploy.sh`: 19/19, with stub `docker` and `curl`. Covered:
  - refusals: a moving name, no name, a malformed sha, a missing env file, an unpublished image, files that would run another tag;
  - deploys: a deploy logged, every service smoke-tested through the front door;
  - `previous`: from its own files, after a rollback, and refused with one release;
  - rollbacks: a failing start, a failing smoke check, a failing rollback;
  - the first release failing, redeploying the running release, and the dry run touching nothing.
- Mutations, each caught by a named case (run in Git Bash):
  - the rollback never runs;
  - `previous` ignores the current release;
  - the name check removed;
  - the registry not asked;
  - smoke results ignored;
  - a foreign tag allowed;
  - the dry run deploys.

  A first round driven from Python reported every mutation "caught" because it ran WSL's bash, which failed every run. It was discarded, and the mutations rerun in Git Bash. "`previous` starting the loop at the current release" survived as an equivalent mutant (the condition still skips it); dropping the condition is caught.
- Overlay negative controls, each caught by `verify-production-overlay.sh`:
  - a database port published;
  - pgAdmin on by default;
  - one hop only;
  - a TOTP secret seeded;
  - the gateway built on the server;
  - an app at `:main`;
  - `ip_range` dropped.

  A control written as `ports: !reset ["5434:5432"]` survived because compose still resets it to nothing. It was rewritten as removing the line, which is caught.
- actionlint: `deploy.yml` is clean. shellcheck `-S warning`: clean.
- CI `deploy-dry-run` passes on the PR; `publish` waits for it.
- **For real on this machine** (published images `sha-18af798` and `sha-359dc82`, Caddy's local CA, a fresh `DEPLOY_PATH` and empty volumes):
  1. deploy `sha-18af798`: healthy, 11 smoke checks ok.
  2. deploy `sha-359dc82` with its Caddyfile broken: the containers were healthy, the smoke checks got 502, it **rolled back to `sha-18af798` by itself**, and the shop answered 200 afterwards.
  3. `sha-359dc82` with its files repaired: deployed.
  4. `previous`: resolved to `sha-18af798` and deployed from its own files.

  The release log reads `failed, rolled-back, deployed, deployed` as expected. Its first line, `07:21:30 sha-359dc82 failed`, came from the aborted first attempt's still-running child writing into the new directory, and was not part of this run.
- **Found by the first real attempt**: on empty volumes RabbitMQ's first boot took over two minutes beside every other container starting, past 12 × 10 s of health checks. The first deploy failed on a broker that came up moments later. Fixed with `start_period: 180s`, and the rerun above starts from empty volumes.
- Not exercised: the SSH hop of `deploy.yml`, which needs the owner's server and the four secrets.
