# Tasks: Checkout and Order History

- [X] T001 Order: `CheckoutPricing`, extracted from `SubmitOrderCommandHandler`, which now calls it
- [X] T002 Order: `GetCheckoutQuoteQuery` + validator (shared `MustBeADeliveryOption` rule); `GET /api/orders/quote`
- [X] T003 Order.Tests `CheckoutQuoteTests`: quote == placed order, part by part; quote places and publishes nothing; same refusals as checkout
- [X] T004 Bruno `order/checkout quote`; `checkout` asserts it charged the quoted total
- [X] T005 Client `api/orders.ts`, `CheckoutPage`, `OrderPage` (polling, totals, status sentences), `OrdersPage`
- [X] T006 Docs: CLAUDE.md, jwt-setup.md access table
- [ ] T007 PR `Closes #38`, `Closes #39`; CI green; squash-merge

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
