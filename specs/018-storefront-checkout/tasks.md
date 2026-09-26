# Tasks: Checkout and Order History

> Completed on 2026-09-27, after the feature merged (#48), from the code at that merge, the pull
> request and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `018-storefront-checkout`

**Tests**: Included for the backend change - `CheckoutQuoteTests` against a real PostgreSQL, with a
negative control - and in Bruno. No client tests (none until specs/028).

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 (see it, then pay it), US2 (wait for the saga honestly), US3 (read my orders)

T001-T007 are the list written at the merge, kept as they were.

- [X] T001 Order: `CheckoutPricing`, extracted from `SubmitOrderCommandHandler`, which now calls it
- [X] T002 Order: `GetCheckoutQuoteQuery` + validator (shared `MustBeADeliveryOption` rule); `GET /api/orders/quote`
- [X] T003 Order.Tests `CheckoutQuoteTests`: quote == placed order, part by part; quote places and publishes nothing; same refusals as checkout
- [X] T004 Bruno `order/checkout quote`; `checkout` asserts it charged the quoted total
- [X] T005 Client `api/orders.ts`, `CheckoutPage`, `OrderPage` (polling, totals, status sentences), `OrdersPage`
- [X] T006 Docs: CLAUDE.md, jwt-setup.md access table

## Recorded after the merge

Added on 2026-09-27 from the diff of #48.

- [X] T008 [US1] Register `CheckoutPricing` in `server/src/Services/Order/Ecommerce.Order.Application/DependencyInjection.cs`, and use `MustBeADeliveryOption` in `SubmitOrderCommandValidator.cs` so the order and the quote share the rule
- [X] T009 [US1] Negative control on `The_quote_is_exactly_what_the_same_choices_then_charge`: `TotalAmount + 0.01m` in the quote handler fails it; restored
- [X] T010 [US2] `describeStatus` and `isSettling` in `client/src/api/orders.ts`: sentences for each status, and failure reasons classified by `/stock/i` and `/payment|declin/i` rather than shown raw
- [X] T011 [US2] Polling in `client/src/pages/OrderPage.tsx`: 1000 ms, at most 30 polls, cancelled on unmount; "Thank you for your order" only when just placed and settled
- [X] T012 [P] [US1] Export `Totals` from `OrderPage.tsx` and reuse it in `CheckoutPage.tsx`, so the quote and the order are drawn by the same component
- [X] T013 [US1] A link from the cart to checkout in `client/src/pages/CartPage.tsx`; routes `/checkout`, `/orders`, `/orders/:id` behind `RequireAuth` in `client/src/App.tsx`
- [X] T014 [P] Re-sequence `bruno/order/get my orders.yml` and `get order by id.yml` after the new request
- [X] T007 PR [#48](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/48) `Closes #38`, `Closes #39`; CI green; squash-merged as `84abfc4` on 2026-09-22

## Dependencies & Execution Order

T001 → T002 → T003, T004, T009 on the server. On the client, T005's `api/orders.ts` first, then the
three pages; T010-T013 belong to them. The client needs T002 deployed for the checkout page to price
anything.

## What actually happened

Order.Tests 46/46 (41 + 5). Mutation `TotalAmount + 0.01m` in the quote handler:
`The_quote_is_exactly_what_the_same_choices_then_charge [FAIL]`. Restored afterwards.

End to end through the Vite proxy, against the containerised stack with Order rebuilt:

```text
quote, no address        -> 409
quote, teleport          -> 400 errors.ShippingOption
quote GB express         -> subtotal 10.0, shipping 15.0, tax 5.0 (20%), total 30.0
order after 2s: Paid     -> stored 10.0 / 15.0 / 5.0 / 0.0 / 0.2 / 30.0   (identical)
list                     -> 1 [Paid]; cart after: []
other customer's order   -> 404

Payment restarted with PAYMENT_OUTCOME=Reject:
order after 2s: Failed   -> "Payment declined by the stub gateway ..."; cart after: still 2 units
```

Bruno 54/54, 72 tests. **Not clicked through in a real browser.**

## Notes

- **T007 is listed last although its id is lower**: it was the last task at the merge; T008-T014
  describe work already inside that pull request.
- 14 tasks, all done.
