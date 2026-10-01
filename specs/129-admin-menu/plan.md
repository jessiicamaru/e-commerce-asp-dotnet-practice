# Implementation Plan: The admin console's menu is grouped and shows what is waiting

**Branch**: `feat/246-admin-menu` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #246

## Summary

Group the sidebar's links, add badges from a dedicated `useStaffWaiting` hook (page-size-1 reads under their own keys, enabled by role), and carry the opening list through `state.from` to the order page and the layout's highlight.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: TanStack Query, react-router  
**Storage**: none  
**Testing**: Vitest  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: five small requests per minute for an administrator  
**Constraints**: no server change  
**Scale/Scope**: one layout, one hook, three list pages, one order page  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client only; each count comes from its owner's existing list. |
| **II. Clean Architecture Layering** | **Pass.** Hook in `hooks/admin`, drawing in `layouts/`. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write. |
| **IV. Identity Comes From the Token** | **Pass.** Roles on the session decide only which counts are asked for; the server refuses on its own. |
| **V. Evidence Over Assumption** | **Planned.** Vitest for groups, badges, roles and the highlight, mutation-checked; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/129-admin-menu/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/layouts/admin-layout (+ test)
client/src/hooks/admin
client/src/constants/query-keys
client/src/pages/admin-order
client/src/pages/admin-orders
client/src/pages/admin-order-search
client/src/pages/admin-returns
```

## Complexity Tracking

No violation to justify.
