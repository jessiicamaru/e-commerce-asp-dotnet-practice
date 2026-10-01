# Implementation Plan: Product photographs fill their frame

**Branch**: `feat/251-photo-frames` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #251

## Summary

`ProductImage` large: no fixed aspect - the image keeps its own ratio, width-filling and capped in height, on a 4:3 tinted frame only until it loads; the browser flows upload a 3:2 photograph and measure it.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: Tailwind v4  
**Storage**: none  
**Testing**: Playwright (a real uploaded image); Vitest for the classes per state  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: n/a  
**Constraints**: cards and thumbnails unchanged  
**Scale/Scope**: one component  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client only. |
| **II. Clean Architecture Layering** | **Pass.** One shared component. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write. |
| **IV. Identity Comes From the Token** | **Not applicable.** No request changes. |
| **V. Evidence Over Assumption** | **Planned.** A browser check with a real 3:2 upload, shown red with the square frame; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/125-photo-frames/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/components/product/product-image/index.tsx (+ test)
client/e2e/flows.spec.ts
client/e2e/support/api.ts
```

## Complexity Tracking

No violation to justify.
