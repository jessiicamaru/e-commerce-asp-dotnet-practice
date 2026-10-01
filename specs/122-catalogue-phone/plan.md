# Implementation Plan: The catalogue on a phone

**Branch**: `fix/245-catalogue-phone` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #245

## Summary

Responsive classes only: a two-column grid under `sm`, a compact hero (no featured photo, smaller heading, a scrolling chip row), narrower price inputs, and a loading tint on product images; a Playwright test at 390px.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: Tailwind v4  
**Storage**: none  
**Testing**: Playwright at 390px; Vitest for the image's loading state  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: the first product within two screens  
**Constraints**: desktop unchanged  
**Scale/Scope**: catalogue page, hero, filters, product image  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client only. |
| **II. Clean Architecture Layering** | **Pass.** Classes in the components that draw each part. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write. |
| **IV. Identity Comes From the Token** | **Not applicable.** No request changes. |
| **V. Evidence Over Assumption** | **Planned.** A browser test at 390px shown red with the old grid; a Vitest for the loading tint; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/122-catalogue-phone/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/pages/catalog/index.tsx
client/src/components/catalog/catalog-hero/index.tsx
client/src/components/catalog/catalog-filters/index.tsx
client/src/components/product/product-image/index.tsx (+ test)
client/src/components/product/product-card/index.tsx
client/e2e/flows.spec.ts
```

## Complexity Tracking

No violation to justify.
