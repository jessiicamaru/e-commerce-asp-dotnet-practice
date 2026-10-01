# Implementation Plan: The seller sidebar keeps to its column

**Branch**: `fix/238-seller-sidebar-overflow` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #238

## Summary

The sidebar and the shop card become grids whose one column may shrink (`minmax(0,1fr)`), so a long name truncates instead of widening the column's content; the browser flows give their seller a long shop name and assert the sidebar stays left of the page.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: Tailwind v4  
**Storage**: none  
**Testing**: Playwright (Edge locally) against the stack  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: n/a  
**Constraints**: no server change  
**Scale/Scope**: one layout, one e2e assertion  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client layout only. |
| **II. Clean Architecture Layering** | **Pass.** A layout class change inside `layouts/`. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write. |
| **IV. Identity Comes From the Token** | **Not applicable.** No request changes. |
| **V. Evidence Over Assumption** | **Planned.** A browser assertion with a long name, shown red with the fix removed; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/117-seller-sidebar-overflow/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/layouts/seller-layout/index.tsx
client/e2e/flows.spec.ts
client/e2e/support/api.ts
```

## Complexity Tracking

No violation to justify.
