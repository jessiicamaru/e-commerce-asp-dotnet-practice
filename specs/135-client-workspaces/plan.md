# Implementation Plan: The client becomes a workspace of apps and packages

**Branch**: `refactor/275-client-workspaces` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md) | **Issue**: #275

## Summary

Move the single Vite app into `apps/storefront`. Lift the shadcn kit into `packages/ui`, and everything that is not a
page, layout, route or app component into `packages/core`. Rewrite imports so shared code is named by package. Keep
every `npm` script the same at `client/`.

## Technical Context

**Language/Version**: TypeScript 6, React 19
**Primary Dependencies**: Vite 8, Vitest 5 (projects), Tailwind v4, npm workspaces
**Storage**: none
**Testing**: Vitest (all 693 tests), Playwright flows, the storefront image check
**Target Platform**: the storefront, and next the back office (#276)
**Project Type**: web client (monorepo)
**Performance Goals**: the same bundle
**Constraints**: no visible change; `shadcn add` keeps working; CI's commands unchanged
**Scale/Scope**: about 400 files moved, imports rewritten by script

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client only; the storefront still talks only to the gateway. |
| **II. Clean Architecture Layering** | **Pass.** The client's own layering becomes enforced: packages never import an app (a test checks), and apps import packages by name. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No server change. |
| **IV. Identity Comes From the Token** | **Not applicable.** Session handling moves package, unchanged. |
| **V. Evidence Over Assumption** | **Planned.** The same 693 tests, the browser flows and the image check, before and after. A mutation shows the layering test bites. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/135-client-workspaces/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
docs/architecture/adr-003-storefront-and-back-office.md
```

### Source Code (repository root)

```text
client/
├── package.json            workspaces, scripts that run every workspace
├── tsconfig.base.json      compiler options and the package aliases
├── vitest.config.ts        one run, a project per workspace
├── e2e/, playwright.config.ts, nginx/, Dockerfile   unchanged in role
├── apps/storefront/        index.html, public/, src/{components,pages,layouts,routes,App,main,index.css}
├── packages/ui/            src/ (shadcn kit + cn), components.json
└── packages/core/          src/{config,context,services,hooks,utils,constants,locales,test}
```

## Complexity Tracking

No violation to justify.
