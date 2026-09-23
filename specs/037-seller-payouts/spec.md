# Feature Specification: What the shop owes each seller

**Feature branch**: `037-seller-payouts`
**Created**: 2026-09-23
**Status**: Draft

## What is wrong

Since specs/034-036 a seller sees what they sold, ships their own parcel and is named on it. Nothing
says what they are **owed**. The customer pays the shop the whole order - every seller's goods, one
delivery charge, the tax - and nothing records how much of that belongs to whom:

- the shop takes no commission anywhere, so the marketplace earns nothing and nothing says it should;
- the delivery charge is paid once per order, while each seller posts their own parcel at their own
  cost, and nothing splits it (specs/035 said so and left it: "The delivery charge is not split");
- nothing records that a seller *has been* paid, so paying twice, or never, is indistinguishable.

Payment is still a stub that moves no money (specs/005). A payout here is therefore **a ledger** - an
amount owed and a record that it was settled - not a transfer.

## Decisions taken with the user (2026-09-23)

- **Commission**: one rate for the whole marketplace, set in configuration, **frozen onto the order at
  checkout** so a rate changed tomorrow never rewrites a sale made today.
- **Delivery**: the order's delivery charge is split **equally between its parts**; any remainder in the
  currency's smallest unit goes to the first part. A seller's share is paid to them, because they post
  their own parcel.

## User Scenarios

### US1 - A seller sees what each sale earns them (P1)

**Acceptance**
1. Each of a seller's sales shows the goods total, the commission taken, their share of delivery and the
   amount owed to them (goods − commission + delivery share).
2. The numbers never change after checkout: not when the commission rate is changed, not when a shop
   is renamed, not when the product is re-priced.

### US2 - A seller sees their balance and their payouts (P1)

**Acceptance**
1. A seller sees, per currency: **on the way** (paid by the customer, not shipped yet), **due** (shipped,
   not paid out yet) and **paid out**.
2. A seller sees the payouts made to them: when, how much, in which currency, covering how many sales.
3. A sale whose order failed, or is still settling, counts nowhere.

### US3 - An administrator settles what is due (P2)

**Acceptance**
1. An administrator sees, per seller and currency, what is due now.
2. An administrator records a payout for a seller in a currency; it covers **every** part due at that
   moment, and its amount is the sum of them.
3. A part is covered by **exactly one** payout, even when two administrators record the same payout at
   the same moment - the second is told there is nothing due.
4. Recording a payout when nothing is due is refused, and changes nothing.

### Edge cases

- **The shop's own part** keeps its delivery share; it is never owed to anybody and never paid out.
- **Orders from before this** recorded no rate and no split. They are not in any balance and cannot
  be paid out by this feature: applying today's rate to them would apply today's terms to an older sale.
  The sale still shows, with its earnings marked as not recorded.
- **A part created on demand** (specs/035: an order written by an older image during a rollback) is the
  same: terms not recorded.
- **A single-part order**: that part gets the whole delivery charge.

## Requirements

- **FR-001** At checkout the order MUST record the commission rate in force, and each part its goods
  total, its commission and its delivery share, rounded to the currency's minor unit.
- **FR-002** The delivery shares of an order MUST sum exactly to its delivery charge.
- **FR-003** Commission is charged on the seller's goods before tax. Tax and the commission stay with
  the shop, which charged the customer and remits the tax.
- **FR-004** A part becomes due when it is shipped, and stays due until a payout covers it.
- **FR-005** A payout MUST cover each part at most once, under concurrency.
- **FR-006** A seller MUST see only their own earnings, balance and payouts; not-theirs looks like
  not-there (specs/034 D5).
- **FR-007** Only an administrator records a payout, and a payout records who recorded it.
- **FR-008** The commission rate MUST be configured, at least 0 and below 1, or Order does not start.

## Out of scope

- Moving money: the payment provider is still a stub.
- A per-seller rate, reversing a payout, refunds and returns.
- An administrator page in the storefront (fulfilment has none either); the admin half is API and Bruno.
- Showing the customer how their delivery charge was split.

## Success Criteria

- **SC-001** A two-seller order's delivery shares sum to its delivery charge, to the smallest unit.
- **SC-002** Changing the configured rate leaves every existing sale's earnings unchanged.
- **SC-003** Two simultaneous payouts for the same seller cover every due part exactly once between them.
- **SC-004** `verify-saga.sh` passes; Bruno passes.
