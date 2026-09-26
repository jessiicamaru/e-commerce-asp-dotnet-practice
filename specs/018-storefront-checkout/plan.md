# Implementation Plan: Checkout and Order History

> Written on 2026-09-27, after the feature merged (#48), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Branch**: `018-storefront-checkout` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/018-storefront-checkout/spec.md`

## Summary

Three pages and one backend endpoint. Order's pricing moves, unchanged, out of
`SubmitOrderCommandHandler` into `CheckoutPricing`, and a new `GetCheckoutQuoteQuery` behind
`GET /api/orders/quote` calls the same class and places nothing; the delivery-option validation becomes
one shared rule, `MustBeADeliveryOption`, so the quote refuses what checkout refuses. In the client,
`/checkout` shows Order's own quote for the chosen address and option, then places the order;
`/orders/:id` polls every second while the order is `Submitted`, for at most 30 polls; `/orders` lists
the customer's orders. Status and failure reasons become sentences in `describeStatus`.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 (Order); TypeScript ~6.0 / React 19 (client)

**Primary Dependencies**: MediatR 12.4.1, FluentValidation 12.1.1 (Order); `react-router-dom` (client)

**Storage**: No change. The quote is computed and never stored.

**Testing**: `Ecommerce.Order.Tests/CheckoutQuoteTests` (5 tests, real PostgreSQL, MassTransit harness);
Bruno `order/checkout quote` and `order/checkout`; end to end through the Vite proxy against the
containerised stack, both payment outcomes

**Target Platform**: Order (`:5059`) behind the gateway; the browser via Vite (`:5173`)

**Project Type**: Web front end plus one additive backend endpoint

**Performance Goals**: The order page shows a settled order within one poll (1 s) of settling.

**Constraints**: What is shown must be what is charged; the quote must have no side effect; no
contract or migration change.

**Scale/Scope**: 1 query + handler + validator, 1 extracted class, 1 shared rule, 1 controller action,
1 test class, 2 Bruno requests; 3 client pages and 1 API module.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0, after the merge.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order remains the one owner of what is charged: the client computes nothing and shows Order's quote. The quote reads Cart, Identity and Catalog over the same gRPC paths checkout already used (specs/009-011) - no new cross-service dependency and no database read outside Order |
| **II. Clean Architecture Layering** | **Pass.** `CheckoutPricing`, the query and the shared rule live in Application under `Orders/Common/` and `Orders/Queries/GetCheckoutQuote/`, depending only on the existing interfaces (`ICartReader`, `IAddressReader`, `IShippingOptions`, `ICatalogPrices`, `ITaxRates`); the controller action only dispatches |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The quote writes and publishes nothing - `A_quote_places_nothing_and_publishes_nothing` counts orders and `OrderSubmittedEvent`s before and after. Checkout's own stage-publish-save order is untouched: `CheckoutPricing` runs before anything is staged, as the pricing did inline before |
| **IV. Identity Comes From the Token** | **Pass.** `GetCheckoutQuoteQuery(AddressId, ShippingOption)` carries no user id; the cart and address are read with the caller's forwarded token. Another customer's order stays a 404 (`GET /api/orders/{id}` unchanged) |
| **V. Evidence Over Assumption** | **Pass.** A negative control - skewing the quote's total by 0.01 - turned `The_quote_is_exactly_what_the_same_choices_then_charge` red. Both saga outcomes were driven end to end with the Order container rebuilt, the stored parts compared with the quote. Not clicked through in a real browser, and the pull request says so |

**Post-design re-check**: no violations. The extraction is a move, not a rewrite: the same arithmetic
(`OrderTotals.Compute`) and the same refusals, now reached from two handlers.

## Project Structure

### Documentation (this feature)

```text
specs/018-storefront-checkout/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Five decisions
├── data-model.md        # No table; the quote and the client types
├── quickstart.md        # Tests, Bruno, the end-to-end run, the browser walk-through
├── contracts/
│   └── http-api.md      # GET /api/orders/quote (new) and the order endpoints the pages use
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
server/src/Services/Order/
├── Ecommerce.Order.Application/Orders/Common/CheckoutPricing.cs         # extracted; PriceAsync
├── Ecommerce.Order.Application/Orders/Common/DeliveryOptionRule.cs      # MustBeADeliveryOption
├── Ecommerce.Order.Application/Orders/Queries/GetCheckoutQuote/GetCheckoutQuoteQuery.cs  # query, response, validator, handler
├── Ecommerce.Order.Application/Orders/Commands/SubmitOrder/SubmitOrderCommandHandler.cs  # now calls CheckoutPricing
├── Ecommerce.Order.Application/Orders/Commands/SubmitOrder/SubmitOrderCommandValidator.cs # uses the shared rule
├── Ecommerce.Order.Application/DependencyInjection.cs                  # registers CheckoutPricing
└── Ecommerce.Order.WebApi/Controllers/OrdersController.cs              # [HttpGet("quote")]
server/tests/Ecommerce.Order.Tests/CheckoutQuoteTests.cs
bruno/order/checkout quote.yml (new), checkout.yml, get my orders.yml, get order by id.yml (re-sequenced)

client/src/
├── api/orders.ts            # listShippingOptions, getQuote, placeOrder, getOrder, listMyOrders, isSettling, describeStatus
├── pages/CheckoutPage.tsx
├── pages/OrderPage.tsx      # polling; exports Totals
├── pages/OrdersPage.tsx
├── pages/CartPage.tsx       # a link to checkout
├── App.tsx                  # /checkout, /orders, /orders/:id behind RequireAuth
└── index.css
CLAUDE.md, docs/features/auth/jwt-setup.md (access table)
```

**Structure Decision**: the shared pricing sits in `Orders/Common/`, beside `OrderTotals` (specs/012),
because two use cases call it; the quote follows the CQRS folder convention as a query. The client's
`Totals` component is exported from `OrderPage.tsx` and reused by `CheckoutPage.tsx`, so the quote and
the order are drawn by the same code too.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- **Polling, not push.** "Push can come later" (#34).
- **Failure reasons stay operator text**, classified in the client; a customer-facing reason code is
  recorded as the better contract, not filed.
- **Not clicked through in a real browser** at this merge; #23's final comment says the same of the
  whole storefront.
