# Implementation Plan: A Storefront That Builds and Reaches the Gateway

> Written on 2026-09-27, after the feature merged (#42), from the code at that merge, the pull request
> and docs/architecture/storefront.md (the storefront has no page of its own under docs/features/).

**Branch**: `014-storefront-scaffold` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/014-storefront-scaffold/spec.md`

## Summary

Scaffold `client/` with Vite 8, React 19 and TypeScript 6, deliberately thin. Vite's dev server proxies
`/api` to the gateway, so the browser sees one origin and needs no CORS. `src/api/http.ts` is the call
layer every later page uses: ProblemDetails → `ApiError`, the access token from memory, and one
refresh-and-retry on 401. A status page checks every service's `/health` through the gateway. CI gains a
`client` job ("Storefront build") that runs `npm ci`, oxlint, `tsc -b` and `vite build`. Decisions:
[research.md](./research.md).

## Technical Context

**Language/Version**: TypeScript ~6.0, React ^19.2, targeting the browser

**Primary Dependencies**: `react`, `react-dom`, `react-router-dom` ^7.18; dev: `vite` ^8.3,
`@vitejs/plugin-react` ^6.1, `oxlint` ^1.81, `typescript` ~6.0.2

**Storage**: None. The client stores nothing; the access token (from #35 on) is a module variable.

**Testing**: No unit tests (none until specs/028). CI checks lint, types and build. The dev proxy was
checked by hand against the running containers.

**Target Platform**: A browser, served by Vite's dev server on `:5173`, proxying `/api` to the gateway
on `:5000`

**Project Type**: Web front end, a new top-level project beside `server/`

**Performance Goals**: None.

**Constraints**: Talks only to the gateway. No token in any storage a script can read. No CORS needed
anywhere.

**Scale/Scope**: One page (status), one call layer, one CI job.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0, after the merge. The
principles are written for the services; where one does not reach a front end, the row says so rather
than claiming a pass it did not earn.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The client owns no data and reads no database. It reaches the services only through the gateway, the one public entry point, and never addresses a service's own port |
| **II. Clean Architecture Layering** | **Pass - not applicable to the client's own structure.** No service code changed. The client has its own layering (one call layer under `src/api/`, pages under `src/pages/`), which the principle does not govern |
| **III. Atomic Writes and Idempotent Messaging** | **Pass - not applicable.** No write, no message |
| **IV. Identity Comes From the Token** | **Pass.** The call layer sends identity only as a bearer token, never as a body field, and the token is kept in memory only (the owner's decision on #34), so no script-readable storage holds it. The server still decides everything; nothing in the client grants access |
| **V. Evidence Over Assumption** | **Pass.** The proxy was exercised against the running containers: the page and all six `/api/<svc>/health` routes answered 200 through `localhost:5173`. Build and lint clean. What was not checked is stated: the pages were not opened in a real browser by a person at this point |

**Post-design re-check**: no violations. The decisions that shape later features (in-memory token,
proxy, polling) were taken here once and recorded on #34.

## Project Structure

### Documentation (this feature)

```text
specs/014-storefront-scaffold/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Six decisions, from #34 and the pull request
├── data-model.md        # No table; the client's own types
├── quickstart.md        # Build, lint, dev proxy, status page
├── contracts/
│   └── http-api.md      # What the client calls through the gateway (health only), and the proxy
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
client/
├── package.json, package-lock.json      # scripts: dev, build (tsc -b && vite build), lint (oxlint), preview
├── vite.config.ts                       # port 5173; proxy /api -> GATEWAY_URL ?? http://localhost:5000
├── tsconfig.json, tsconfig.app.json, tsconfig.node.json
├── .oxlintrc.json, .gitignore, index.html, public/favicon.svg
├── README.md
└── src/
    ├── main.tsx, App.tsx, index.css
    ├── api/http.ts                      # api<T>(), ApiError, configureAuth()
    └── pages/StatusPage.tsx

.github/workflows/ci.yml                 # job `client`, "Storefront build", Node 22
CLAUDE.md, docs/README.md
```

**Structure Decision**: a sibling of `server/` at the repository root, as #34 decided ("one PR can
change an endpoint and its only consumer together, and CI checks both"). One file per page under
`src/pages/` and one call layer under `src/api/` - the later redesign (client/README.md's conventions)
replaced this layout.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- **Nothing to buy yet**: every shopping page is a later sub-issue of #23.
- **`configureAuth` exists but nothing configures it** until #35; until then every call is anonymous
  and a 401 is never retried.
- **The Orchestrator is missing from the status page** - it had no health route (specs/071 added one).
- **No deployment**: the storefront image came with specs/051; the CI job publishes nothing.
