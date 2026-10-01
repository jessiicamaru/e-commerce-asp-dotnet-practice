# Implementation Plan: Order lines fit any width

**Branch**: `fix/239-order-lines-narrow` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #239

## Summary

`OrderLines` stops being a three-column table and becomes a list of two-column grid rows (`minmax(0,1fr) auto`): details on the left with quantity × price under them, the total on the right. `OrderTotals` puts its total row in a subgrid wrapper so the rule is continuous, and sits at the right.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: Tailwind v4  
**Storage**: none  
**Testing**: Vitest (content) and Playwright (layout)  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: n/a  
**Constraints**: no server change  
**Scale/Scope**: two shared components  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client only. |
| **II. Clean Architecture Layering** | **Pass.** Shared components in `components/order`. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write. |
| **IV. Identity Comes From the Token** | **Not applicable.** No request changes. |
| **V. Evidence Over Assumption** | **Planned.** Vitest for what each line says, a browser assertion for where its total is, both shown red; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/118-order-lines-narrow/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/components/order/order-lines/index.tsx (+ test)
client/src/components/order/order-totals/index.tsx (+ test)
client/e2e/flows.spec.ts
```

## Complexity Tracking

No violation to justify.
