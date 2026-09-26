# Implementation Plan: A Cart and an Address Book

> Written on 2026-09-27, after the feature merged (#47), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Branch**: `017-storefront-cart` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/017-storefront-cart/spec.md`

## Summary

Three client pieces over existing endpoints. An `AddToCart` control on the product page (or a sign-in
link that returns to the product); `/cart`, which lists Cart's lines with the prices Catalog gives
today, sends every change at once and reads the cart again; `/addresses`, the address book kept by
Identity, with per-field errors. Typed calls in `src/api/cart.ts` and `src/api/addresses.ts`. Both pages
sit behind `RequireAuth`, and the top bar gains a Cart link. No backend change.

## Technical Context

**Language/Version**: TypeScript ~6.0 / React 19

**Primary Dependencies**: `react-router-dom`; the call layer (specs/014) and the auth context (specs/015)

**Storage**: None in the client. The cart lives in Cart (`ecommerce_cart_db`), addresses in Identity
(`delivery_addresses`); neither schema changed.

**Testing**: No client tests (none until specs/028). The calls were exercised through the Vite proxy
with a newly registered customer; lint and build in CI.

**Target Platform**: Browser via Vite (`:5173`), gateway on `:5000`

**Project Type**: Web front end only

**Performance Goals**: None. A change costs one write and one cart read.

**Constraints**: The client computes no total; the cart is never stored in the browser.

**Scale/Scope**: 2 pages, 1 component, 2 API modules, 2 routes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0, after the merge.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The cart's names and prices are Catalog's, delivered through Cart; the page shows them and decides nothing - no total is computed in the client, and the estimate is labelled as one. Addresses stay Identity's |
| **II. Clean Architecture Layering** | **Pass - not applicable.** No service code changed |
| **III. Atomic Writes and Idempotent Messaging** | **Pass - not applicable.** The writes are the existing Cart and Identity commands, unchanged |
| **IV. Identity Comes From the Token** | **Pass.** Every call carries only the bearer token; no request names a user. `POST /api/cart/items` takes `productId` and `quantity`, never a user id - the rule `SubmitOrderCommand` established |
| **V. Evidence Over Assumption** | **Pass, with the gap named.** Each call was exercised through the proxy with a new customer, including a refused quantity (-1 → 400), the cart surviving sign-out and sign-in, and per-field address errors; the output is recorded. Not clicked through in a real browser |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/017-storefront-cart/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Four decisions
├── data-model.md        # No table; the client types
├── quickstart.md        # The proxy run and a browser walk-through
├── contracts/
│   └── http-api.md      # The Cart and address endpoints relied on; none changed
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
client/src/
├── api/cart.ts              # getCart, addToCart, setQuantity, removeLine, emptyCart, lineProblem
├── api/addresses.ts         # listAddresses, createAddress, updateAddress, deleteAddress, makeDefault, describe
├── pages/ProductPage.tsx    # + AddToCart
├── pages/CartPage.tsx
├── pages/AddressesPage.tsx
├── App.tsx                  # routes /cart and /addresses behind RequireAuth; Cart link in the top bar
└── index.css
```

**Structure Decision**: as specs/014 set it - one module per backend area, one file per page.
`AddToCart` lives in `ProductPage.tsx`, the only place that uses it.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- **Checkout** (specs/018).
- **An anonymous cart** - a backend feature nobody has asked for since.
- **`emptyCart` is exported but no page calls it** at this merge.
- **Not clicked through in a real browser** at this merge.
