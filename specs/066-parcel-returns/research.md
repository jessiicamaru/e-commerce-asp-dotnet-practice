# Research: Returning a delivered parcel (part 1 - the server)

> Written on 2026-09-27, after the feature merged (#149), from the code at that merge, the pull request and
> docs/features/returns.md.

**Feature**: [spec.md](spec.md) | **Date of the decisions**: 2026-09-25 (decision 50 in
[docs/project/decisions.md](../../docs/project/decisions.md))

Seven decisions shaped the design. Who took each one beyond "recorded in the pull request" is not recorded, except
where the PR says the issue expected otherwise.

> An alternative marked *(reconstructed)* was not recorded when the decision was taken; it is the obvious other
> choice, named in the backfill so the decision reads whole, and its reason for rejection is inferred from the code
> and the recorded rationale.

---

## D1 - What is returned: whole parcels

**Decision**: A return is of one whole parcel (one `order_shipments` row), never of single lines or quantities.

**Rationale**: It matches whole-order cancellation (specs/039): one refund, one restock, one state per return and no
per-line states. A parcel is already the unit a seller ships and a buyer confirms, so it is the unit a buyer can send
back.

**Alternatives considered**:

- **Per line, or per quantity.** Rejected for now: every step would need per-line states, the refund would need to be
  computed and capped per line, and Inventory would need to be told partial quantities - all for a case nobody had
  asked for yet. Recorded as a known limit.

---

## D2 - When a seller's money is due: a hold, never a debt

**Decision**: A seller's part is due only once it was delivered **more than the return window ago** and no return of
it is open. A returned part is no money at all. A return can start only inside the window.

**Rationale**: If a return can only start inside the window and money only becomes due after it, then nothing already
paid out can ever come back: there are no negative balances and nothing to claw back. The cost is that every seller
is paid 7 days later than before (specs/040 made money due at delivery).

**Alternatives considered**:

- **Pay at delivery and turn a returned, paid-out part into a debt** - what issue #107 expected. Rejected: the shop
  would hold a claim against a seller with no mechanism to collect it, and every balance could go negative.
- *(reconstructed)* **Pay at delivery and refuse returns of paid-out parcels.** Rejected: the buyer's right to return would depend on
  when an administrator happened to record a payout.

---

## D3 - Which returns are "open"

**Decision**: Open means `Requested`, `Escalated` or `SentBack`; or `Accepted`/`Refused` with `DecidedAt` still inside
the window. `Rejected` and a lapsed `Accepted`/`Refused` release the money; `Received` removes the part from money
altogether.

**Rationale**: A state the buyer can still act on must hold the money, and one they can no longer act on must not, or
a buyer who never escalates would freeze a seller's money for ever. A `Requested` return never lapses on its own: a
seller who does not answer keeps their own money held, which is the incentive to answer.

**Alternatives considered**:

- *(reconstructed)* **Any return row holds the money.** Rejected: a final rejection or an abandoned acceptance would hold it for ever.
- *(reconstructed)* **Lapse unanswered requests too.** Rejected: it would reward a seller for ignoring the buyer.

---

## D4 - "Due" written twice, held together by a test

**Decision**: The LINQ projection `PayoutRepository.Money` serves the balance and the due list; the payout claim's CTE
states the same rule in SQL (`DeliveredAt <= cutoff` and `NOT EXISTS` an open return). The test
`The_payout_claims_exactly_what_the_balance_calls_due` asserts the two agree.

**Rationale**: The claim must stay one statement (specs/037 research D5): its `WHERE` is re-evaluated under each row's
lock, which is what makes two concurrent payouts safe. It cannot reuse a LINQ query. The balance is a `GroupBy` with
conditional sums, which is naturally LINQ. Two expressions of one rule are acceptable only when a test fails the
moment they drift - and the mutation run showed it does (both "the balance ignores an open return" and "the claim
ignores an open return" were caught).

**Alternatives considered**:

- *(reconstructed)* **One SQL view read by both.** Rejected: the claim's guard has to be inside the `UPDATE ... WHERE` to be evaluated
  under the lock; a view read beforehand would reintroduce the race.

---

## D5 - What is refunded: goods plus tax, never delivery

**Decision**: The refund is the parcel's lines as frozen at checkout, `UnitPrice × Quantity + TaxAmount`, in the
order's currency. The delivery share is not refunded.

**Rationale**: The prices frozen on the order (specs/009, specs/012) are what was paid; computing from them keeps the
refund exact whatever the catalogue says now. The customer pays for the return trip, and nothing in the system
models return shipping.

**Alternatives considered**:

- **Refund the parcel's delivery share too.** Rejected: the delivery happened; the return trip is the customer's
  cost.
- **Let Payment compute the amount.** Rejected: only Order knows which lines were in the parcel and what was paid for
  them. Payment instead checks the amount against what it owns (D6).

> Later change, recorded so the next reader is not misled: specs/069 (vouchers) made the refund goods less any
> voucher discount plus tax - what was actually paid. At this merge there were no discounts.

---

## D6 - How the refund and the restock stay once-only

**Decision**: `ParcelReturnedEvent` carries its lines and its amount. Payment keys the refund on a new, unique
`refunds.ReturnId`; the old unique index on `refunds.OrderId` becomes partial (`WHERE "ReturnId" IS NULL`). Inventory
claims `returned_parcels(ReturnId)` with `ON CONFLICT DO NOTHING` before moving stock, in the same transaction.

**Rationale**: Idempotency by database constraint, as Principle III requires. Unlike `OrderCancelledEvent` (which
carries nothing because each consumer reads its own records), a return is *part* of an order, so the event must say
which part. Payment still checks what it owns - an approved payment, the same currency, and refunds never exceeding
the amount taken - and refuses and logs anything else.

Found while building: the unique index on `refunds.OrderId` would have made a second parcel's refund, or a return
refund beside a cancellation refund, collide; and `GetRefundsAsync` built a dictionary keyed by order id that would
throw on two refunds of one order. Both were fixed in this change.

**Alternatives considered**:

- *(reconstructed)* **Key the refund on `(OrderId, ShipmentId)`.** Rejected: `ReturnId` names the thing being refunded, and a parcel has
  at most one return anyway.
- **Rely on the MassTransit inbox alone.** Rejected by the constitution: configuration alone is not a guarantee.

---

## D7 - Who handles returns

**Decision**: The parcel's seller answers and receives their own parcel. An administrator answers and receives the
shop's own parcels, and has the final word on an escalated return of anyone's. Returns are `Admin`, not `Moderator`.

**Rationale**: The fulfilment endpoints are `Admin` (specs/039), and a return is the end of fulfilment. An escalated
return is out of the seller's hands, or the dispute would be decided by the party being disputed.

**Alternatives considered**:

- *(reconstructed)* **Staff decide every return.** Rejected: the seller has the goods and the knowledge; staff are the appeal.
- **Moderators too (`StaffRoles.Staff`).** Rejected for consistency with fulfilment and payouts, which are money.

---

## Open risks

| Risk | Impact | Mitigation |
| :-- | :-- | :-- |
| "Due" drifts between LINQ and SQL | A payout claims what the balance does not show, or the reverse | `The_payout_claims_exactly_what_the_balance_calls_due`, mutation-checked |
| Every seller is paid 7 days later | Sellers notice a change in cash flow | Intended; stated in the PR and in `fulfilment-and-delivery.md` |
| A variant deleted between sale and return | Units cannot be put back | Logged as a warning, not invented; tested by `A_variant_with_no_stock_row_is_skipped_not_invented` |
