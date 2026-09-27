# Feature Specification: Administrators manage delivery and the carrier

**Feature Branch**: `098-delivery-settings` | **Created**: 2026-09-27 | **Issue**: #196

**Status**: Merged (#205, 2026-09-27)

**Input**: Issue #196 - "delivery options and the carrier can only be changed by redeploying".

## Why

What delivery costs and which options exist live in `appsettings.json` (`Shipping:Options`, specs/011, 022). Changing
the express price in dong means a new image. `ConfiguredShippingOptions` says why it was built that way - "two rows that
rarely change do not need screens" - and the management audit of 2026-09-27 reverses that: the shop now has staff who
run it from the console, and a price is theirs to change.

The shop has **one carrier** (decided with the user: no courier role, no second carrier). It has no name and no
tracking page: the tracking reference a seller or the shop enters is free text, shown as text, and a customer copies it
into the carrier's website by hand.

## User Scenarios & Testing *(mandatory)*

### US1 - An administrator changes what delivery costs (Priority: P1)

`/admin/delivery` lists every delivery option - its name, a price per currency, whether it is offered, its order - and
an administrator edits them. The next quote and checkout use the new values; an order already placed keeps the name and
price it froze (specs/011).

**Why this priority**: The heart of the issue.

**Independent Test**: Change express's dong price; the next quote charges it; an order placed before still says the old
price.

**Acceptance Scenarios**:

1. **Given** an option, **When** an administrator sets a new price in a currency, **Then** quotes and checkouts in that
   currency use it.
2. **Given** an option with no price in a currency, **Then** it is not offered in that currency (the specs/022 rule).
3. **Given** a price the currency cannot hold (9.5 dong), a negative price or an unknown currency, **Then** 400.
4. **Given** an order placed before the change, **Then** it keeps its frozen option name and price.

---

### US2 - An administrator adds, withdraws and orders options (Priority: P2)

A new option is created by saving under a new code; an option is withdrawn by turning it off (it is never deleted - old
orders name it); options are offered in the administrator's order.

**Why this priority**: Adding "same-day" or pausing "overnight" are the other common changes.

**Acceptance Scenarios**:

1. **Given** a new code, **When** saved, **Then** the option exists and is offered where priced.
2. **Given** an option turned off, **Then** it is neither listed to shoppers nor accepted at checkout (400 as for an
   unknown option).
3. **Given** the last option still offered, **When** an administrator turns it off, **Then** 409 - a shop with no delivery
   cannot sell.
4. **Given** an option offered, **Then** it has a price in the shop's default currency (400 otherwise).

---

### US3 - The carrier has a name and a tracking link (Priority: P1)

An administrator names the one carrier and gives its tracking address as a template (`https://.../track/{reference}`).
Wherever a tracking reference is shown - the customer's order, the seller's sale, the staff order, a returned parcel -
it becomes a link to the carrier's page, with the carrier's name.

**Why this priority**: The customer-facing half of the issue.

**Independent Test**: Set a template; a shipped parcel's reference on the order page is a link to it.

**Acceptance Scenarios**:

1. **Given** a template containing `{reference}`, **Then** each reference links to the template with the reference
   filled in (escaped).
2. **Given** no template, **Then** references are shown as text, as today.
3. **Given** a template that is not an absolute http(s) address or has no `{reference}`, **Then** 400.

---

### US4 - Configuration still starts a fresh shop (Priority: P2)

A new database is seeded from `Shipping:Options` and `Shipping:Carrier`; afterwards the table is the truth. A code in
configuration that the table does not have yet is added at startup; nothing an administrator changed is overwritten.

**Why this priority**: Every environment (compose, CI, tests) starts from configuration.

**Acceptance Scenarios**:

1. **Given** an empty table, **When** the service starts, **Then** the configured options and carrier are stored.
2. **Given** an option an administrator repriced, **When** the service restarts, **Then** the administrator's price
   stays.

### Edge Cases

- **Two instances starting at once.** The seed inserts with `ON CONFLICT DO NOTHING`; both succeed, one row each.
- **An option's code.** Fixed once created (lower-case letters, digits, hyphens, ≤ 32) - it is what checkout sends.
- **A currency removed from `Money:Supported` later.** Its price rows stay but are never offered (unsupported currencies
  are refused at the request).
- **Instances and freshness.** Checkout reads the table per request, so every instance sees an edit at once.
- **The option names' language.** One name per option, as configuration had (Out of scope: translated names).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Delivery options (code, name, offered, order) and their prices per currency are stored in Order's database;
  checkout, quotes and `GET /api/orders/shipping-options` read them per request.
- **FR-002**: `GET /api/orders/delivery` (Admin) returns every option - offered or not - and the carrier.
- **FR-003**: `PUT /api/orders/delivery/options/{code}` (Admin) creates or updates an option: name (1-100), offered,
  order, prices (supported currencies, ≥ 0, fitting the currency's minor unit); an offered option needs a price in the
  default currency; turning off the last offered option is 409. Audited.
- **FR-004**: `PUT /api/orders/delivery/carrier` (Admin) sets the carrier's name (1-100) and tracking template (null, or an
  absolute http(s) URL containing `{reference}`, ≤ 500). Audited.
- **FR-005**: `GET /api/orders/delivery/carrier` (anyone) returns the carrier's name and template, so every page can link a
  tracking reference.
- **FR-006**: At startup, configured option codes and the carrier missing from the database are inserted; nothing
  existing is changed.
- **FR-007**: Orders keep what they froze at checkout; nothing already placed changes.
- **FR-008**: `/admin/delivery` edits options and the carrier; every tracking reference in the storefront is a link when
  a template is set.

### Key Entities

- **Delivery option** - code, name, offered, order; **price** per currency.
- **Carrier** - the one delivery partner: name, tracking template.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Order tests: a repriced option is what the next quote charges and an older order keeps its price; an option
  turned off is refused at checkout; the last offered option cannot be turned off; the validators' refusals; the seed
  inserts missing codes only; the carrier's template is validated.
- **SC-002**: Storefront tests: the delivery page saves options and the carrier; a tracking reference renders as a link
  with the template, and as text without one.
- **SC-003**: Bruno: an administrator reads and saves delivery settings; a customer is 403; the public carrier read is 200.
- **SC-004**: Mutations - checkout reading configuration instead of the table, the last-option guard removed, the seed
  overwriting existing rows - each red.

## Decision

1. **The table, not configuration, is the truth; configuration seeds it.** Reverses specs/011's "no screens" choice, now
   that staff run the shop from the console. Insert-missing-only means a restart never undoes an administrator's edit
   ([research.md](research.md) D1).
2. **One carrier, with a URL template.** Decided with the user: the shop has one delivery partner; a courier role and
   carrier choice are out. A template turns every reference into a link without storing a URL per parcel (D3).
3. **Read per request, synchronously.** Keeps `IShippingOptions` - used inside validators - synchronous and every instance
   current, at the cost of one small query per checkout (D2).

## Assumptions

- `Money:Supported` and `Money:DefaultCurrency` are as today (VND default, USD).

## Out of scope

- Translated option names; a courier role or several carriers; per-seller delivery options.
