# Implementation Plan: A cart before signing in

**Branch**: `feature/370-guest-cart` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md) | **Issue**: #370

## Summary

The browser keeps a signed-out shopper's lines; Cart prices them anonymously with the code that prices a stored cart
(extracted into `CartPricing`) and merges them into the account's cart on sign-in, taking the larger quantity, in one
transaction. The storefront reads either cart through the same hooks, shows the guest count, opens `/cart` signed out,
and merges wherever a sign-in ends.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: MediatR, FluentValidation; TanStack Query
**Storage**: none new on the server; `localStorage` in the browser
**Testing**: `GuestCartTests` (Cart, PostgreSQL); Vitest; Bruno; a browser check
**Constraints**: nothing stored without an owner; the merge idempotent; checkout unchanged
**Scale/Scope**: one extraction, two commands/queries, two actions; a browser store, hooks, the header, the cart page,
the add button, a merge component

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Cart prices through its existing Catalog read; no new edge, no other service told. |
| **II. Clean Architecture Layering** | **Pass.** Pricing and the merge in Application, the lock and save through the existing repository and unit of work, thin controller. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The merge is one transaction under the cart's lock, and idempotent by its rule (research D2). Pricing writes nothing. No message. |
| **IV. Identity Comes From the Token** | **Pass.** The merge's owner is `ICurrentUser`; the body has no user id. The anonymous pricing touches no one's cart. |
| **V. Evidence Over Assumption** | **Pass.** Pricing equal to the stored cart's, the merge rule and its repeat on PostgreSQL; the guest journey in a browser. |
| **Schema compatibility** | **Pass.** No migration. |

**Post-design re-check**: unchanged - see `tasks.md`.

## Project Structure

```text
specs/162-guest-cart/
server/src/Services/Cart/
  Ecommerce.Cart.Application/Carts/CartPricing.cs                 (extracted from GetMyCart)
  Ecommerce.Cart.Application/Carts/GuestCart.cs                    (PriceLinesQuery, MergeCartCommand, validators)
  Ecommerce.Cart.Application/Carts/Queries/GetMyCart/*             (uses CartPricing)
  Ecommerce.Cart.WebApi/Controllers/CartController.cs              (price, merge)
server/tests/Ecommerce.Cart.Tests/GuestCartTests.cs
client/packages/core: services/cart, hooks/cart, utils/cart (the browser store), locales
client/apps/storefront: add-to-cart, top-bar, pages/cart, routes, a merge component in the layout
bruno/cart/
```

## Complexity Tracking

None.
