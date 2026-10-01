# Implementation Plan: Notice wording is edited like the emails

**Branch**: `feat/250-notice-wording-layout` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #250

## Summary

`AdminWordingPage` becomes a two-column layout - kinds on the left (names from `wording.kindName.*`), the chosen kind's sentences with `WordingEditor` open on the right - with the language in a `TabStrip`, and the choice in the address.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: react-router, react-i18next  
**Storage**: none  
**Testing**: Vitest  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: n/a  
**Constraints**: no server change; the editor unchanged  
**Scale/Scope**: one page, 76 labels  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client only. |
| **II. Clean Architecture Layering** | **Pass.** A page and its words. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** Writes unchanged. |
| **IV. Identity Comes From the Token** | **Not applicable.** No request changes. |
| **V. Evidence Over Assumption** | **Planned.** Vitest for the layout, the address and the names, mutation-checked; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/128-notice-wording-layout/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/pages/admin-wording/index.tsx (+ test)
client/src/locales/{en,vi}/admin.json
```

## Complexity Tracking

No violation to justify.
