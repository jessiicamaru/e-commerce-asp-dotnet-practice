# Feature Specification: Two Price Lists, Not One Price Converted

> Completed on 2026-09-27, after the feature merged (#59), from the code at that merge, the pull request, docs/features/catalog.md and docs/features/shopping-and-checkout.md.

**Feature Branch**: `022-multi-currency-prices` · **Created**: 2026-09-22 · **Status**: Implemented, merged as [#59](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/59) on 2026-09-22 (see [tasks.md](tasks.md) for what building it found)

**Input**: the owner asked for two kinds of price, dollars and dong.

## Why this exists

The shop has amounts. It does not have money.

Nothing anywhere says what unit `Price` is in, and the numbers already disagree with each other. A
product is listed at `40000000`; the delivery options in
[Order's appsettings](../../server/src/Services/Order/Ecommerce.Order.WebApi/appsettings.json) cost
`5.0` and `15.0`. Read as one currency, a camera costs forty million and getting it to your door
costs five. Read as two, the order total adds them together anyway. The payments table records
`Amount 40000005.00` and has no column that would say of what.

So this is not "add a currency picker". It is the same shape of defect as issue #18 and as `UserId`
before it: **a number the system acts on whose meaning nobody wrote down**. Two shoppers can be
shown the same digits and be charged two different amounts of money, and no row in any database
would be wrong, because no row claims anything.

The second reason is the one that was asked for: the shop is Vietnamese and sells to people who read
in dollars. Those people need a price in dollars that somebody **decided**, not one that an exchange
rate produced at the moment they happened to click.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A price is an amount and a currency, together (Priority: P1)

Every amount the shop shows or stores says what currency it is in, and nothing adds two amounts in
different currencies.

**Why this priority**: it is the correctness half. Without it the rest is decoration on an ambiguity.

**Independent Test**: read any price, any quote and any order, and each carries a currency; place an
order and the payment row records the same currency as the order.

**Acceptance Scenarios**:

1. **Given** any product, quote or order read over the API, **Then** the response says which currency
   its amounts are in.
2. **Given** an order placed in a currency, **When** the payment is recorded, **Then** the payment
   says the same currency - a recorded amount with no currency is a record of nothing.
3. **Given** an order placed before this feature, **When** it is read, **Then** it reports the shop's
   default currency rather than claiming to know something it does not.

---

### User Story 2 - The dollar price is decided, not computed (Priority: P1)

An administrator sets a price per currency for a variant. A shopper reading in dollars sees the
dollar price that was set.

**Why this priority**: the thing asked for, and the decision that makes the rest honest. A shop that
divides by a rate at checkout charges a different amount every morning, for reasons no customer can
see and no administrator chose.

**Independent Test**: set a variant's VND and USD prices to two numbers that are not a conversion of
each other; read the variant in each currency; each price comes back exactly as entered.

**Acceptance Scenarios**:

1. **Given** a variant priced in both currencies, **When** it is read in either, **Then** the amount
   set for that currency comes back unrounded and unconverted.
2. **Given** a variant priced in VND only, **When** it is read in USD, **Then** it is shown as **not
   sold in this currency** - never as a converted amount, and never as the VND number relabelled.
3. **Given** a variant not priced in the checkout's currency, **When** a customer tries to buy it,
   **Then** the checkout refuses and names the variant.

> The contrast with translations (specs/021) is deliberate and is the heart of this feature. A
> missing translation falls back to the other language and the worst case is a shopper reading
> English. **A missing price must not fall back to the other currency**: the worst case is charging
> 40,000,000 dollars for a camera, or selling it for 1,600 dong.

---

### User Story 3 - The shopper chooses a currency, and it is not their language (Priority: P2)

A shopper picks VND or USD independently of whether they read Vietnamese or English, and the choice
survives a reload.

**Why this priority**: the visible half. Tying it to language would be cheaper and wrong - a
Vietnamese person reading English still pays in dong, and a visitor reading Vietnamese may want to
see dollars.

**Independent Test**: read the shop in Vietnamese with prices in USD, and in English with prices in
VND; both work.

**Acceptance Scenarios**:

1. **Given** any page, **When** the shopper switches currency, **Then** every amount on it changes,
   and the language does not.
2. **Given** a chosen currency, **When** the shopper returns later, **Then** it is still chosen.
3. **Given** an amount shown in VND, **Then** it is formatted with no decimal places; **given** USD,
   with two - because that is how many each currency has.

---

### User Story 4 - An order remembers what it charged (Priority: P2)

An order records its currency alongside its amounts, and reading it later in another currency does
not restate it.

**Why this priority**: the same reason an order freezes its prices, its words and its address. An
order is a record of a purchase.

**Independent Test** (added 2026-09-27): place an order in VND, then read it with `X-Currency: USD`;
every amount still reads in VND and the response says `currency: "VND"`.

**Acceptance Scenarios**:

1. **Given** an order placed in VND, **When** it is opened by a shopper whose currency is USD,
   **Then** it still reads in VND.
2. **Given** the delivery charge on that order, **Then** it is in the order's currency, not in
   whatever the configuration says today.

---

### Edge Cases

(Added 2026-09-27 from the code, the tests and the PR.)

- **An unsupported currency code** (`?currency=EUR`) falls back to the default, like an unsupported
  language, rather than 404.
- **A request that asks for nothing** reads the default currency, exactly as before the feature
  (`A_request_that_asks_for_nothing_reads_the_default_currency`).
- **Setting the default currency's price** writes `product_variants.Price` itself; there is no `VND` row,
  and removing the default currency's price is refused with 409.
- **A price of zero** is refused: zero is a price. **A price the currency cannot hold** (9.99 dong) is
  refused on every command that sets one (research D8).
- **Stored prices from before the feature** that the currency cannot hold keep their amounts; validation is
  on writes.
- **A message from an older Order or saga** carries no currency (`""`); Payment reads it as the default,
  because on the day of the deploy that is the truth (research D4).
- **An Orchestrator image not rebuilt** would drop the currency on the relay - the specs/020 defect on the
  field where it cannot be detected afterwards.
- **A delivery option with no price in the checkout's currency** is not offered, and choosing it anyway is
  refused.
- **A product none of whose variants is priced in the currency** is still listed, with no "from" price.

## Key Entities

- **Currency**: a code with a number of decimal places (`VND` 0, `USD` 2), from configuration.
- **Variant price**: an amount a person set for one variant in one currency. No row means not sold in it.
- **An order's currency**: frozen at checkout; every amount on the order is in it.
- **A payment's currency**: the currency of the amount the payment row records.

## Requirements *(mandatory)*

- **FR-001**: Every amount crossing a boundary - HTTP response, gRPC message, broker message,
  database row - MUST be accompanied by the currency it is in.
- **FR-002**: A price MUST be stored per currency and returned as stored. The system MUST NOT convert
  between currencies at any point in a request.
- **FR-003**: A variant with no price in the requested currency MUST NOT be sellable in it, and the
  refusal MUST name it.
- **FR-004**: The currency MUST be chosen independently of the language, and MUST default to the
  shop's configured default when nothing is asked for.
- **FR-005**: Amounts MUST be rounded to the requested currency's minor unit - two decimal places for
  USD, **zero** for VND - wherever the system computes one (tax, line totals, delivery).
- **FR-006**: An order MUST freeze the currency it was placed in, and every amount on it MUST be in
  that currency.
- **FR-007**: A payment record MUST carry the currency of the amount it records.
- **FR-008**: Delivery options MUST have a price per currency. A delivery option with no price in the
  checkout's currency MUST NOT be offered.
- **FR-009**: Everything that exists MUST keep working: an order, a product and a service image from
  before this feature must all still read correctly, and a request that asks for no currency must
  behave exactly as it does today.

## Success Criteria *(mandatory)*

- **SC-001**: A product priced in two currencies returns two amounts that are not a conversion of
  each other, proving no rate was applied.
- **SC-002**: A checkout in USD produces an order, a quote and a payment row that all say USD and all
  carry the same total.
- **SC-003**: A VND total contains no fractional dong anywhere in its parts, and the parts still sum
  to the total under the existing CHECK constraint.
- **SC-004**: An automated check fails if an amount is ever paired with the wrong currency by the
  saga - the relay defect that specs/020 hit with `VariantId` is the same shape, and this feature
  crosses the same relay.

Measured at the merge (from the PR): SC-001 by `VariantPriceTests` with 40,000,000₫ against $1,499; SC-002
on the running stack - a USD checkout produced `payments.Amount 6597.80, Currency USD`; SC-003 by
`A_dong_total_has_no_fractional_part_and_the_parts_still_sum` and on the stack (tax a whole `3,003`); SC-004
only **half-automated**: `The_currency_travels_with_the_amount_to_the_saga` proves Order puts the currency on
the event, while the saga's relay into `ProcessPaymentCommand` was verified only by reading the payments row
on the stack - the Orchestrator had no test project at this merge, and the one added in specs/053 does not
assert the currency either (see tasks T036).

## Assumptions

- Two currencies, `VND` and `USD`, with `VND` the default. A third is rows and configuration, not a
  migration - the same test the translation design had to pass.
- No historical exchange rate is stored, because none is applied. If the shop later wants reporting
  across currencies, that is a reporting concern with its own rate table and its own date.
- Administrators enter prices; nothing generates them. The migration that introduces the dollar list
  seeds it from the dong list at one stated rate on one stated day, as **initial data an
  administrator is expected to edit**, and that is an editorial act rather than a mechanism.

## Out of scope

- Any runtime conversion, rate feed, or "approximate price in your currency" display.
- Per-country currency defaults from IP or address - the address is known too late (it is chosen
  mid-checkout) and would change the prices a shopper already saw.
- Multi-currency reporting, settlement or accounting.
- Currency for anything an administrator sees while administering.

## Decisions taken before building

1. Where the currency is decided, and how a request asks for one.
2. Whether a price list hangs off the product or the variant.
3. What happens when a variant is not priced in the requested currency, in listings and at checkout.
4. How the currency reaches Payment through the saga, given the relay defect this project already hit.
5. How rounding to a currency's minor unit interacts with the tax computation and the CHECK
   constraint on the order's parts.
6. What the storefront stores, and how it formats two currencies in two languages.
