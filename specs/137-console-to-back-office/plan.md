# Implementation Plan: The admin and moderator console moves to the back office

**Branch**: `feat/277-console-to-back-office` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md) | **Issue**: #277

## Summary

Move 21 console pages, the console's layout and their tests from `apps/storefront` to `apps/back-office`, rewriting
`/admin/...` to `/...`. Lift the 36 components both apps draw into `packages/core/src/components`. Give each app the
other's address at run time. The storefront redirects its old console addresses, links staff to the back office and
sends console notices there. Closing a shop moves to the back office.

## Technical Context

**Language/Version**: TypeScript 6, React 19
**Primary Dependencies**: react-router 7, TanStack Query, react-i18next; nginx (`envsubst` templates)
**Storage**: none
**Testing**: Vitest (the moved tests, plus redirects, links and app addresses), Playwright (approval in the back office)
**Target Platform**: storefront and back office
**Project Type**: web client (monorepo)
**Performance Goals**: the storefront's bundle loses the console
**Constraints**: no server change; every console behaviour is preserved; old addresses and notices keep working
**Scale/Scope**: about 21 pages, 1 layout and 36 components moved by script

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** No service changes. Both apps still talk only to the gateway. |
| **II. Clean Architecture Layering** | **Pass.** Shared components go to a package that imports no app. The layering test covers them, and one test now covers both apps. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No writes added. |
| **IV. Identity Comes From the Token** | **Pass.** Roles still only draw. The back office's guard and the per-page role checks move unchanged; the server decides every request. |
| **V. Evidence Over Assumption** | **Planned.** The moved tests, new tests for redirects, notice links and app addresses, Playwright in the back office, and mutations. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/137-console-to-back-office/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/apps/back-office/src/{pages/admin-*,layouts/admin-layout,routes}     moved from the storefront
client/packages/core/src/components/{insights,order,product,question,seller,shared,voucher}/...   36 shared components
client/packages/core/src/config/apps                                      the other app's address
client/apps/*/public/app-config.js, client/nginx/default.conf.template     run-time addresses
client/apps/storefront/src/{routes,components/layout,pages/account,pages/shop-front}
client/e2e/
```

## Complexity Tracking

No violation to justify.
