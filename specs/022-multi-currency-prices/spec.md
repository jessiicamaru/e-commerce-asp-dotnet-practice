# Feature Specification: Two Price Lists, Not One Price Converted

**Feature Branch**: `022-multi-currency-prices` · **Created**: 2026-09-22 · **Status**: Implemented (see [tasks.md](tasks.md) for what building it found)

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

**Acceptance Scenarios**:

1. **Given** an order placed in VND, **When** it is opened by a shopper whose currency is USD,
   **Then** it still reads in VND.
2. **Given** the delivery charge on that order, **Then** it is in the order's currency, not in
   whatever the configuration says today.

---

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
