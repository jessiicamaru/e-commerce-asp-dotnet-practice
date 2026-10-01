# Implementation Plan: The cart empties on screen when the order is paid

**Branch**: `fix/242-cart-after-payment` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #242

## Summary

`useOrder` remembers whether it has seen the order settling; when the status leaves the settling states it invalidates the cart query, and again after a short delay to cover Cart's consumer finishing after Order's.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: TanStack Query  
**Storage**: none  
**Testing**: Vitest (hook) and Playwright (badge)  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: two extra cart reads per checkout  
**Constraints**: no server change  
**Scale/Scope**: one hook  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The client asks Cart again; Cart stays the owner of what the cart holds. |
| **II. Clean Architecture Layering** | **Pass.** In the hooks layer. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** No write. |
| **IV. Identity Comes From the Token** | **Not applicable.** No request changes. |
| **V. Evidence Over Assumption** | **Planned.** A hook test and a browser assertion, each shown red without the change; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/119-cart-after-payment/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/hooks/order/index.ts (+ index.test.tsx)
client/src/constants/order
client/e2e/flows.spec.ts
```

## Complexity Tracking

No violation to justify.
