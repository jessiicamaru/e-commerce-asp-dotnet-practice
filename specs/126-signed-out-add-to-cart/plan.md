# Implementation Plan: A signed-out shopper is offered Add to cart

**Branch**: `feat/252-signed-out-add-to-cart` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #252

## Summary

`AddToCart` renders for everybody and, signed out, sends the shopper to sign in with the product's address (and the chosen variant) and a reason; the product page reads `?variant=`; the sign-in page words the reason; the SKU waits for a choice.

## Technical Context

**Language/Version**: TypeScript, React 19  
**Primary Dependencies**: react-router  
**Storage**: none  
**Testing**: Vitest + Testing Library  
**Target Platform**: the storefront  
**Project Type**: web client  
**Performance Goals**: n/a  
**Constraints**: no server change; D10 kept  
**Scale/Scope**: three components  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Not applicable.** Client only. |
| **II. Clean Architecture Layering** | **Pass.** Component, page and sign-in page. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** Nothing written before sign-in. |
| **IV. Identity Comes From the Token** | **Pass.** The cart is still the signed-in customer's, from the token. |
| **V. Evidence Over Assumption** | **Planned.** Vitest with mutations; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/126-signed-out-add-to-cart/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
client/src/components/product/add-to-cart/index.tsx (+ test)
client/src/pages/product/index.tsx (+ test)
client/src/pages/sign-in/index.tsx (+ test)
client/src/locales/{en,vi}/*.json
```

## Complexity Tracking

No violation to justify.
