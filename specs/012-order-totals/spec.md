# Feature Specification: A Total With Something Behind It

> Completed on 2026-09-27, after the feature merged (#32), from the code at that merge, the pull
> request and docs/features/shopping-and-checkout.md.

**Feature Branch**: `012-order-totals`

**Created**: 2026-09-22

**Status**: Implemented - merged in [#32](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/32) on 2026-09-22 (01:41, UTC+7)

**Input**: Issue [#21](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/21) — "an order total is one number with nothing behind it"

## Why this exists

An order has one number on it. Since feature 011 that number is the goods plus delivery, but nothing
on the order says so, there is no tax at all, and a receipt would read "you paid X" with nothing
underneath. The number is also what payment takes — so whatever arithmetic produced it *is* the
charge, and a customer cannot be shown how it was reached.

## Decisions already taken

The project owner asked for the recommended option to be chosen rather than asked (2026-09-22):

- **Catalogue prices exclude tax.** Tax is added on top and shown as its own line. Recorded as
  ADR-002 with the rejected alternative (tax-inclusive prices).
- **Tax is computed by Order**, from a rate per destination country held in its configuration, with
  a default rate for countries not listed. Order already owns what is charged; a separate tax service
  would own one small table.
- **Tax applies to goods and to delivery.**
- **Rounding**: tax is computed per line and on delivery separately, each rounded to 2 decimals with
  halves rounded away from zero, then summed.
- **Discounts are out of scope as a feature**, but the total keeps a discount part — always zero for
  now — so a voucher later does not mean rebuilding the total.
- **The rate applied is stored on the order**, so a rate changed tomorrow never changes what was
  agreed today.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A customer sees what they are paying for (Priority: P1)

A customer checking out, and later reading their order, sees the goods subtotal, delivery, tax,
discount and grand total as separate figures, and the rate of tax applied.

**Why this priority**: It is the feature — a total a customer can check.

**Independent Test**: Check out three items at 9.99 with express delivery to a destination taxed at
10%, and read the order.

**Acceptance Scenarios**:

1. **Given** a checkout, **When** the order is read, **Then** it shows subtotal, delivery, tax,
   discount and grand total, and the tax rate applied.
2. **Given** any order placed by this feature, **When** its parts are added up, **Then** subtotal +
   delivery + tax − discount equals the grand total exactly.
3. **Given** an order line, **When** it is read, **Then** it shows the tax charged on that line.

---

### User Story 2 - Tax follows the destination (Priority: P1)

The same cart sent to two destinations with different rates is taxed differently.

**Why this priority**: Equal first; tax that ignores the destination is not tax.

**Independent Test**: Check out identical carts to two countries with different configured rates;
compare.

**Acceptance Scenarios**:

1. **Given** two destinations with different rates, **When** the same cart is checked out to each,
   **Then** the tax differs accordingly and the subtotals are equal.
2. **Given** a destination with no rate of its own, **When** checked out, **Then** the default rate
   applies, and the order says which rate that was.

---

### User Story 3 - The charge is exactly the stored total (Priority: P1)

Payment takes exactly the grand total recorded on the order — never a figure recomputed later.

**Why this priority**: A total the customer agreed to and a different amount taken would be the
worst failure a shop can have.

**Independent Test**: Check out; compare what payment was asked for with the stored grand total.
Then change the configured rate and read the order again.

**Acceptance Scenarios**:

1. **Given** a checkout, **When** payment is taken, **Then** the amount is the stored grand total to
   the cent.
2. **Given** an order already placed, **When** the configured rate changes, **Then** the order's tax
   and total are unchanged.

---

### User Story 4 - The rules are written down and cannot drift (Priority: P2)

The rounding rule and the choice that prices exclude tax are documented with their reasons, and the
database refuses a total whose parts do not add up.

**Why this priority**: Second, because it guards the first three rather than adding to them — but
this is exactly what nobody can reconstruct a year later.

**Independent Test**: Run `OrderTotalsTests` and `TotalsPersistenceTests`; insert an order whose parts
do not sum and watch the database refuse it; open `docs/architecture/adr-002-tax-exclusive-prices.md`.

**Acceptance Scenarios**:

1. **Given** a line whose tax falls exactly on half a cent, **When** tax is computed, **Then** it
   rounds away from zero, and a test says so.
2. **Given** an attempt to store an order whose parts do not sum to its total, **When** saved, **Then**
   the database refuses it.

### Edge Cases

- **Orders placed before this feature** have no stored parts. They are given a subtotal equal to
  their total less delivery, zero tax and zero discount — which is what they were — rather than a tax
  that was never charged.
- **A rate of 0** is valid (a destination with no tax) and produces tax 0.00, not a missing figure.
- **Many lines rounding the same way**: per-line rounding can differ from rounding the sum by a few
  cents; that difference is accepted and documented, not "corrected".
- **A misconfigured rate** (negative, or 100% or more) stops the service at startup.
- **A missing default rate** stops the service at startup too - a destination with no rate of its own
  would otherwise have nothing to fall back to.
- **An image from before this feature** keeps inserting orders during a rollback, with no parts at
  all. The database lets those rows through rather than refusing the checkout (plan, schema
  evolution).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every order placed MUST store subtotal, delivery, tax, discount and grand total as
  separate figures, and the tax rate applied.
- **FR-002**: Subtotal + delivery + tax − discount MUST equal the grand total, enforced by the
  database as well as by code.
- **FR-003**: Tax MUST be computed from the order's destination country, using a configured rate for
  that country or a configured default.
- **FR-004**: Catalogue prices MUST be treated as excluding tax; tax MUST be shown as its own figure.
- **FR-005**: Tax MUST apply to each line and to delivery, each rounded to 2 decimals with halves away
  from zero, and the rounded amounts summed.
- **FR-006**: Each order line MUST store the tax charged on it.
- **FR-007**: The discount MUST be stored and MUST be zero; no way to apply a discount is offered.
- **FR-008**: Payment MUST be asked for exactly the stored grand total.
- **FR-009**: Changing a configured rate MUST NOT change any order already placed.
- **FR-010**: Rates MUST be validated at startup: each at least 0 and below 1, and a default present.
- **FR-011**: Orders placed before this feature MUST remain readable, with parts that describe what
  was actually charged (no tax).
- **FR-012**: The choice that prices exclude tax, the rejected alternative, and the rounding rule
  MUST be recorded as an architecture decision.

### Key Entities

- **Order total** — subtotal, delivery, tax, discount, grand total, and the tax rate applied; all
  fixed at checkout.
- **Order line tax** — the tax charged on one line, as rounded.
- **Tax rate table** — a rate per destination country and a default, in configuration.

## Success Criteria *(mandatory)*

- **SC-001**: For every order placed by this feature, the stored parts sum to the stored total in 100%
  of cases, and the database refuses any row where they do not.
- **SC-002**: The same cart to two differently taxed destinations produces different tax and equal
  subtotals.
- **SC-003**: The amount payment is asked for equals the stored grand total to the cent in 100% of
  end-to-end runs.
- **SC-004**: Changing a rate after an order is placed changes that order's figures in 0 cases.
- **SC-005**: The half-cent rounding case has an automated test.

## Assumptions

- One currency, as today.
- A rate per **country** is enough; regional taxes (US states) are later work.
- Tax registration, invoices and tax-exempt customers are out of scope.
- Delivery prices remain flat per option (feature 011); only their tax is new.

## Out of Scope

- Vouchers, coupons and any way of applying a discount.
- Tax-inclusive display, multi-currency, regional or product-category tax rates.
- Invoices and tax reporting.
