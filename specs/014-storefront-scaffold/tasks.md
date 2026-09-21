# Tasks: A Storefront That Builds and Reaches the Gateway

- [X] T001 Scaffold `client/` — Vite 8, React 19, TypeScript 6, oxlint, react-router
- [X] T002 `vite.config.ts`: proxy `/api` to the gateway (`GATEWAY_URL`, default `http://localhost:5000`)
- [X] T003 `src/api/http.ts`: ProblemDetails → `ApiError`, bearer token from memory, refresh-and-retry on 401
- [X] T004 Status page: every service's `/health` through the gateway
- [X] T005 CI job `client`: `npm ci`, lint, type-check and build
- [X] T006 Docs: `client/README.md`, CLAUDE.md, docs index
- [ ] T007 PR `Closes #34`; CI green; squash-merge

## What actually happened

- Dev proxy verified against the running containers: the page and all six health routes answer 200
  through `http://localhost:5173/api/...`.
- `npm run build` (tsc + vite) and `npm run lint` (oxlint) clean.
- The orchestrator cannot appear on the status page: it has no HTTP surface, so no health route — the
  same gap CLAUDE.md records.
