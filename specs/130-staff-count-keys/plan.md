# Implementation Plan: Staff counts never share a list's cache

**Branch**: `fix/268-moderation-count-keys` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md) | **Issue**: #268

## Summary

Point both pages at `useStaffWaiting`, add the page size to two list keys, and invalidate the counts on a decision.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: TanStack Query  
**Storage**: none  
**Testing**: Vitest with one QueryClient across two pages  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: two fewer requests per dashboard visit  
**Constraints**: no server change  
**Scale/Scope**: two pages, two keys, two hooks  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client only. |
| **II. Clean Architecture Layering** | **Pass.** Hooks and keys in their layers. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write. |
| **IV. Identity Comes From the Token** | **Not applicable.** No request changes. |
| **V. Evidence Over Assumption** | **Planned.** A test across two pages sharing a cache, mutation-checked; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/130-staff-count-keys/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/constants/query-keys
client/src/hooks/moderation
client/src/hooks/shop-applications
client/src/pages/admin-moderation (+ test)
client/src/pages/admin-overview
```

## Complexity Tracking

No violation to justify.
