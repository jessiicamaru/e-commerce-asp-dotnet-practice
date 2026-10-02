# Implementation Plan: A back office for staff, with its own sign-in

**Branch**: `feat/276-back-office-app` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md) | **Issue**: #276

## Summary

Add `apps/back-office`, a Vite app sharing `packages/core` and `packages/ui`, with sign-in, a staff guard and a
greeting. Lift the sign-in form, the query-state messages and the theme into the packages so both apps draw the same
ones. Parameterise the Dockerfile by app, add a compose service and an image, and extend CI and Playwright.

## Technical Context

**Language/Version**: TypeScript 6, React 19
**Primary Dependencies**: Vite 8, Tailwind v4, TanStack Query, react-router, react-i18next
**Storage**: none (session: Identity's refresh cookie, host-only)
**Testing**: Vitest (a back-office project), Playwright (a back-office flow), the image check
**Target Platform**: `portal.localhost:5174` in development, `:8089` in compose
**Project Type**: web client (monorepo app)
**Performance Goals**: n/a
**Constraints**: no change to Identity or the gateway's code; the storefront behaves exactly as before
**Scale/Scope**: one new app (about 10 files), three components lifted into packages, one image

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** No service changes. The back office talks only to the gateway, like the storefront. |
| **II. Clean Architecture Layering** | **Pass.** Shared components go to the packages, which still never import an app (the layering test covers the new app too). |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No writes added. |
| **IV. Identity Comes From the Token** | **Pass.** The guard reads roles to *draw*. Every request is still decided by the server. The session is the back office's own cookie. |
| **V. Evidence Over Assumption** | **Planned.** Vitest for the guard and the sign-in form in both apps, Playwright for a real sign-in with a code, the image check. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/136-back-office-app/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/apps/back-office/           index.html, vite.config.ts, vitest.config.ts, src/{main,App,routes,pages,layouts}
client/packages/core/src/components/{sign-in-form,query-state}
client/packages/ui/src/theme.css
client/Dockerfile                  ARG APP
client/e2e/back-office.spec.ts
server/docker-compose.app.yml      back-office service, trusted proxy
.github/workflows/ci.yml, .github/scripts/{verify-storefront-image,prune-images}.sh
```

## Complexity Tracking

No violation to justify.
