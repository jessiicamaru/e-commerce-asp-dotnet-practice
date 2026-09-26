# Feature Specification: Vouchers (part 1 - the server)

> Completed on 2026-09-27, after the feature merged (#153), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Feature Branch**: `069-vouchers` | **Created**: 2026-09-26 | **Issue**: #108 (part 1; part 2 is the screens)

**Status**: Merged (#153, 2026-09-26). Part 2 is [specs/070](../070-voucher-screens/).

## Why

An order's total has had a discount part since specs/012, and it has always been zero. There is no way for
the shop or a seller to lower a price for a campaign, reward a first order, or give free delivery.

The user agreed the design on 2026-09-26:
- shop vouchers and platform vouchers;
- one voucher stored as composable parts (conditions, targets, amounts per currency, limits);
- only the server computes the discount.

## User Scenarios

### US1 - A customer checks out with codes (P1)

The quote and the order take up to 5 voucher codes. The server checks each code and computes the discount,
split across the lines it applies to. It then charges tax on the discounted prices and shows every applied
voucher with its amount.

**Acceptance**:
1. "Orders over 1,000,000₫ get 30% off, capped at 200,000₫" gives exactly that on an eligible order. On
   999,999₫ it is refused with a 409 that names the minimum.
2. "20% off one variant" discounts only that variant's line.
3. "New customers" is refused to anybody who has bought before.
4. Free delivery takes the delivery charge, and its tax, off.
5. The quote and the order agree to the unit. The discount part is non-zero. The CHECK on the order's parts
   holds.
6. A code that is used up, expired, disabled, not yet started, unknown, or not offered in the order's
   currency is refused with a 409 in words.
7. Two customers taking the last use at once: exactly one order is placed, and the other gets a 409.
8. A failed or cancelled order gives its uses back, once.

**Why this priority**: The discount is the point of a voucher; without the checkout taking codes, nothing a shop or
seller creates changes what anybody pays.

**Independent Test**: Create a platform voucher as an administrator, ask for a quote with its code, place the order
with the same code, and compare: the charge equals the quote, the order's `DiscountTotal` is the voucher's amount, and
the voucher's `UsedCount` is 1 (`VoucherCheckoutTests`, Bruno `order/`).

**Acceptance Scenarios**:

1. **Given** a platform voucher of 30% capped in VND with a VND minimum, **When** a customer quotes an order above the
   minimum, **Then** 30% comes off up to the cap; **When** below, **Then** 409 naming the minimum.
2. **Given** a voucher targeting one variant, **When** the cart holds that variant and others, **Then** only that line
   is discounted; a `Product` target takes every variant of the product.
3. **Given** a `NewCustomer` voucher and a customer with a sold order, **When** they use it, **Then** 409.
4. **Given** a free-delivery voucher, **When** it is applied, **Then** the delivery and the tax on it come off, up to
   the voucher's cap.
5. **Given** a quote with codes, **When** the order is placed with the same codes, **Then** both agree to the unit and
   the parts still sum to the total.
6. **Given** a code that is unknown or disabled, **When** it is used, **Then** both read "Voucher X cannot be used." -
   the same words.
7. **Given** a voucher with one use left, **When** many checkouts race for it, **Then** exactly one order is placed and
   the rest get 409 with nothing saved.
8. **Given** an order that used a voucher, **When** it fails or is cancelled, **Then** the voucher's and the customer's
   counts go down by one, once, however often the release runs.
9. **Given** more than 5 codes, **When** they are sent, **Then** 400 (validator) or 409 (pricing) - never applied.

---

### US2 - The shop and sellers create vouchers (P1)

- An **administrator** creates platform vouchers. They apply to every line, from any seller, and the shop
  funds them.
- A **seller** creates shop vouchers. They apply to that seller's lines only, and **the seller funds them**:
  the discount comes out of the seller's payout.
- Both can list their own vouchers and disable one. Somebody else's voucher is a 404.

**Why this priority**: Without a way to create vouchers, US1 has nothing to apply. P1 alongside it.

**Independent Test**: As a seller, create a voucher, list it, disable it, disable it again; as another seller, disable
it; as an administrator, create one and list: the seller's is theirs, a second disable is 409, another seller gets
404, the administrator sees only the platform's (`VoucherManagementTests`, Bruno `seller/`).

**Acceptance Scenarios**:

1. **Given** an administrator, **When** they create a voucher, **Then** it is the platform's (`SellerId` null); **given**
   a seller, **Then** it is their shop's. No request names whose.
2. **Given** a code already taken in any case, **When** another voucher is created with it, **Then** 409; codes are
   stored upper-case.
3. **Given** a seller, **When** they create a free-delivery voucher, **Then** 400 - free delivery is the platform's.
4. **Given** a voucher with no amount row, a percentage outside 1-100, a fixed value in a percent voucher, or an amount
   dong cannot hold, **When** it is created, **Then** 400 and nothing is saved.
5. **Given** seller A's voucher, **When** seller B disables it, **Then** 404; **when** an administrator does, **Then** it
   is disabled; **when** anybody disables it again, **Then** 409.
6. **Given** a customer or no token, **When** they call `/api/vouchers`, **Then** 403 or 401.

---

### US3 - Money stays right downstream (P1)

- A seller's `GoodsTotal`, and so their commission and payout, is their goods **less their own shop
  voucher**. A platform voucher does not reduce it.
- Returning a parcel refunds what was actually paid for it: goods less discount, plus tax.
- A seller's insights count their revenue after their own discount.

**Why this priority**: A discount that reaches the customer but not the ledger pays sellers for money nobody received,
or refunds more than was charged.

**Independent Test**: Place an order with a shop voucher and a platform voucher; the seller's part has `GoodsTotal` less
the shop voucher only; a return of that parcel refunds goods less discount plus tax; the seller's revenue is less their
voucher and not the platform's.

**Acceptance Scenarios**:

1. **Given** a shop voucher of seller A and a platform voucher on one order, **When** A's part is written, **Then**
   `GoodsTotal` is A's goods less A's voucher; the platform's is the shop's cost.
2. **Given** a returned parcel that was discounted, **When** it is received, **Then** the refund is goods less discount
   plus tax.
3. **Given** A's sale with A's voucher and a platform voucher, **When** A reads insights, **Then** revenue is less A's
   voucher and not the platform's.

### Edge Cases

- **A shop voucher with none of its goods in the cart** applies to nothing and is refused in words.
- **A fixed amount larger than the lines it applies to** is capped at them - never a negative line.
- **A delivery that is already free** refuses a free-delivery voucher.
- **Codes in any case** match; duplicates in one request count once.
- **Dollars** round to cents half away from zero; dong has no minor unit.
- **A discount larger than its line** reaching the totals is a programming error, not a refund - it throws.
- **One customer racing themselves** past their per-customer limit gets one order.
- **The pricing check passed but the limit was reached meanwhile**: the guarded claim refuses it and nothing is saved.
- **A returned parcel** does not give its use back; only a failed or cancelled order does.

## Rules

- **Stacking:** at most one platform voucher on the goods, one free-delivery voucher, and one voucher per
  shop. Shop vouchers are taken off first, then the platform voucher applies to what remains.
- **Money per currency, never converted** (specs/022). A voucher is usable only in a currency it has an
  amount row for, even a percentage one, because the cap and the minimum are amounts.
- **Rounding:** every amount is rounded to the currency's minor unit, half away from zero. A voucher's
  discount is spread over its lines in proportion to their price, and the remainder goes to the largest line.
- **A percentage** is 1 to 100 and is capped by `MaxDiscount` when one is set. **A fixed amount** is never
  more than the lines it applies to.
- **Conditions, all of which must hold:**
  - the minimum spend (`MinSubtotal`, per currency, measured over the lines the voucher applies to);
  - `NewCustomer`: no sold order before;
  - `FirstOrderInShop`: no sold order from that shop before (shop vouchers only);
  - `MinQuantity`: units on the lines the voucher applies to.
- **Targets:** none means everything the voucher may touch. Otherwise the voucher applies to `Product` or
  `Variant` ids. `Category` is **not** in part 1 (research D4).
- **Limits:**
  - a start and an optional end;
  - `TotalLimit` and `PerCustomerLimit`, each claimed in a guarded statement inside the order's own
    transaction.

  A failed or cancelled order releases its uses. A returned parcel does not.
- **Frozen on the order:** each line's `ShopDiscount` and `PlatformDiscount`, and a redemption row per
  voucher with its code and amount. Editing or disabling a voucher later changes no order.

## Requirements

### Functional Requirements

- **FR-001**: The quote and the order MUST accept up to 5 voucher codes and apply them through one pricing function, so
  they agree to the unit.
- **FR-002**: Each code MUST be checked for status, dates, total and per-customer limits, currency, conditions and
  targets, and refused with a 409 that says why; an unknown code MUST read like a disabled one.
- **FR-003**: Discounts MUST follow the stacking, order, rounding and allocation rules above.
- **FR-004**: Tax MUST be charged on the discounted price; the order's parts MUST still sum to its total.
- **FR-005**: A voucher's uses MUST be claimed by guarded statements in the order's own transaction; if any claim moves
  nothing, nothing is saved.
- **FR-006**: A failed or cancelled order MUST give its uses back exactly once.
- **FR-007**: An administrator MUST be able to create platform vouchers and a seller shop vouchers; whose it is MUST come
  from the token.
- **FR-008**: Owners MUST be able to list their vouchers and disable one; another's MUST be 404, a second disable 409.
- **FR-009**: A seller's goods total, commission and payout MUST be less their own voucher and not the platform's.
- **FR-010**: A returned parcel's refund MUST be what was paid for it after discount, plus tax.
- **FR-011**: A seller's insights revenue MUST be less their own voucher.
- **FR-012**: Each line's discounts and each applied voucher MUST be frozen on the order and shown on the order and the
  quote.
- **FR-013**: Creating and disabling a voucher MUST be recorded in the audit log.

### Key Entities

- **Voucher**: a code, whose (the platform or one seller), a benefit (percent, fixed amount, free delivery), dates,
  limits and status; composed of **conditions**, **targets** and **amounts per currency**.
- **Customer use count**: how many times one customer holds a voucher on a live order.
- **Redemption**: one voucher applied to one order - its code, name, whose, benefit and amount frozen - released when
  the order fails or is cancelled.
- **Line discounts**: the shop and platform discount frozen on each order line.

## Success Criteria

- **SC-001**: The quote and the order agree to the unit with vouchers applied - `The_quote_and_the_order_agree…` and,
  live, the Bruno checkout whose charge equals the quote.
- **SC-002**: Of many checkouts racing for a voucher's last use, exactly one order is placed
  (`Of_many_checkouts_racing_for_the_last_use_exactly_one_is_placed`).
- **SC-003**: A release run twice gives one use back (`Releasing_an_order_twice_gives_its_use_back_once`).
- **SC-004**: Every rule is killed by a mutation check: 12 of 12 caught.
- **SC-005**: Order tests 260/260 (40 new); Bruno 229/229 requests, 376/376 tests.

## Assumptions

- Prices exclude tax (ADR-002), and tax is per line and on delivery at the destination's rate (specs/012).
- The order's currency is fixed at checkout (specs/022); a voucher is never converted.
- "Sold" is the Overview's definition (Paid, Completed, Preparing, Shipped) for the customer-history conditions.
- Payment is still a stub; the charge is what the saga asks Payment to take.

## Out of scope (part 1)

- The storefront screens (part 2).
- Category targets.
- Editing a voucher. Disable it and create a new one.
- A public list of the vouchers a shopper could use.
- Shop-funded free delivery.
