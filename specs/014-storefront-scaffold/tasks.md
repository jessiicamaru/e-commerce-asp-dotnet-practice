# Tasks: A Storefront That Builds and Reaches the Gateway

> Completed on 2026-09-27, after the feature merged (#42), from the code at that merge, the pull
> request and docs/architecture/storefront.md (the storefront has no page of its own under
> docs/features/).

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `014-storefront-scaffold`

**Tests**: No unit tests - the client had none until specs/028. CI's lint, type-check and build are the
automated check (T005).

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 (starts and reaches the backend), US2 (caught in CI), US3 (one call layer)

T001-T007 are the list written at the merge, kept as they were.

- [X] T001 Scaffold `client/` — Vite 8, React 19, TypeScript 6, oxlint, react-router
- [X] T002 `vite.config.ts`: proxy `/api` to the gateway (`GATEWAY_URL`, default `http://localhost:5000`)
- [X] T003 `src/api/http.ts`: ProblemDetails → `ApiError`, bearer token from memory, refresh-and-retry on 401
- [X] T004 Status page: every service's `/health` through the gateway
- [X] T005 CI job `client`: `npm ci`, lint, type-check and build
- [X] T006 Docs: `client/README.md`, CLAUDE.md, docs index

## Recorded after the merge

Added on 2026-09-27 from the diff of #42.

- [X] T008 [P] [US1] Routes and top bar in `client/src/App.tsx`: `/` (a placeholder home), `/status`, and a not-found route
- [X] T009 [P] [US2] Lint configuration `client/.oxlintrc.json` and the three TypeScript configs `client/tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json`, so `npm run build` runs `tsc -b` before `vite build`
- [X] T010 [US3] `configureAuth(provider, refresh)` in `client/src/api/http.ts`: the seam through which #35 hands the call layer its in-memory token and its refresh function
- [X] T011 [US1] Say on the status page why the Orchestrator is absent (no HTTP surface) in `client/src/pages/StatusPage.tsx`
- [X] T007 PR [#42](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/42) `Closes #34`; CI green; squash-merged as `cf0c234` on 2026-09-22

## Dependencies & Execution Order

T001 first; T002, T003, T008, T009 in parallel after it; T004 needs T002 (the proxy); T005 needs T009;
T010 is part of T003.

## What actually happened

- Dev proxy verified against the running containers: the page and all six health routes answer 200
  through `http://localhost:5173/api/...`.
- `npm run build` (tsc + vite) and `npm run lint` (oxlint) clean.
- The orchestrator cannot appear on the status page: it has no HTTP surface, so no health route — the
  same gap CLAUDE.md records.

## Notes

- **T007 is listed last although its id is lower**: it was the last task at the merge; T008-T011
  describe work already inside that pull request.
- 11 tasks, all done.
