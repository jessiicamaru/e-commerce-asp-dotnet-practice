# Implementation Plan: Deploy and roll back from CI

**Branch**: `feat/288-deploy-rollback` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md) | **Issue**: #288

## Summary

The work is split between the server and the workflow.

- **`server/deploy/deploy.sh`** runs on the server, in the directory of one release's compose files. It:
  - validates the name and checks the registry;
  - pulls, then runs `up -d --wait`;
  - smoke-tests through Caddy, appends to a release log, and on failure puts the previous release back.
- **`.github/workflows/deploy.yml`** (`workflow_dispatch`) resolves the release, takes that commit's compose files,
  copies them over SSH and runs the script.

A CI job runs the same script as a dry run on every pull request, after rendering and asserting the production
overlay. A plain-bash test drives the script's decisions with stub `docker` and `curl`.

## Technical Context

**Language/Version**: bash; GitHub Actions; Docker Compose v2.24+
**Primary Dependencies**: `docker compose up --wait`, `docker manifest inspect`, curl, OpenSSH, `flock` where present
**Storage**: `releases.log` and one directory per release under `DEPLOY_PATH` on the server
**Testing**: `server/deploy/test-deploy.sh` (stubs); `verify-production-overlay.sh` (rendered config); a real
deploy → deploy → rollback on this machine with published images
**Target Platform**: one Linux server; this Windows machine (Git Bash + Docker Desktop) for verification
**Project Type**: deployment tooling
**Performance Goals**: a deploy finishes within the health wait (`DEPLOY_WAIT_SECONDS`, 600 by default)
**Constraints**:
- no secret in the repository or the workflow log;
- nothing deployable named by `:main`;
- no SSH host trusted on first use.
**Scale/Scope**: one script, one workflow, one CI job, one test script, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service still migrates its own database at startup; the deploy starts containers and never reaches into a database. |
| **II. Clean Architecture Layering** | **Not applicable.** No service code changes. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No writes or messages. A deploy can be re-run safely: pulling and `up` with the same tag are idempotent, and the log only gains a line. |
| **IV. Identity Comes From the Token** | **Not applicable.** |
| **V. Evidence Over Assumption** | **Planned.** The script's decisions are tested with stubs, including the automatic rollback. The overlay is rendered and asserted in CI. Real published releases are deployed, swapped and rolled back on this machine. The SSH hop is the one part not exercised, and the record says so. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/142-deploy-rollback/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/deploy/deploy.sh                       runs on the server
server/deploy/test-deploy.sh                  its decisions, with stub docker and curl
.github/scripts/verify-production-overlay.sh  renders the overlay from the shipped files and asserts it
.github/workflows/deploy.yml                  workflow_dispatch: release, dry_run
.github/workflows/ci.yml                      job deploy-dry-run
docs/infrastructure/production.md             §8 deploying and rolling back
```

## Complexity Tracking

No violation to justify.
