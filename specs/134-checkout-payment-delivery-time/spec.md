# Feature Specification: Checkout says how payment works and how long delivery takes

**Feature Branch**: `feat/253-checkout-payment-delivery-time` | **Created**: 2026-10-02 | **Issue**: #253

**Status**: Draft

**Input**: Issue #253 - "checkout says how payment works and how long delivery takes" - found in the screen review of 2026-10-01 (cust-03).

## Why

Checkout asks a customer to choose between Standard and Express by price alone, with no idea of how long either takes,
and it places an order without one word about payment: when the money is taken, what happens if it is refused, and -
the part this shop must never leave out - that the payment provider is a stand-in that moves no money.

## User Scenarios & Testing *(mandatory)*

### US1 - Each delivery option says how long it takes (Priority: P1)

A customer choosing a delivery option sees "3-5 days" or "1-2 days" under its name.

**Acceptance Scenarios**:

1. **Given** an option with an estimate, **When** checkout lists it, **Then** its name reads with "x-y business days" (or "x business days" when both ends are the same).
2. **Given** an option with no estimate, **Then** nothing is said - never "0 days".
3. **Given** a fresh installation, **Then** the configured options (`Shipping:Options`) bring their estimate with them; an option already stored keeps its own.

---

### US2 - An administrator sets the estimate with the option (Priority: P1)

**Acceptance Scenarios**:

1. **Given** `/admin/delivery`, **When** an administrator gives an option "from 2 to 4 days" and saves, **Then** the next checkout shows it, and the audit log records the change.
2. **Given** only one end filled, a negative number, more than 60 days, or a minimum above the maximum, **Then** the save is refused in words naming the field.
3. **Given** both ends cleared, **Then** the option no longer says a time.

---

### US3 - Checkout says how payment works (Priority: P1)

**Acceptance Scenarios**:

1. **Given** checkout, **Then** a Payment card says the order is charged once, in full, when it is placed; that a refused payment takes nothing and puts the goods back; and that a cancelled order is refunded in full.
2. **Given** the payment service reports a stand-in provider (`provider` starting "Stub" on its `/health`), **Then** the card says so plainly: this shop's payments are a demonstration and no money is moved.
3. **Given** the payment service cannot be asked, **Then** the card still says how payment works, and says nothing either way about the provider.

### Edge Cases

- An order freezes its delivery option's name and price; the estimate is a promise made at checkout and is **not** frozen on the order (see research D3).
- The quote's option carries the estimate as it does the name, from the same stored row.
- A disabled option's estimate is kept, and comes back when it is turned on again.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `delivery_options` gains `MinDays` and `MaxDays`, both null or both set, `0 <= MinDays <= MaxDays <= 60` (a CHECK).
- **FR-002**: `PUT /api/orders/delivery/options/{code}` accepts `minDays` and `maxDays`; `GET /api/orders/delivery` and `GET /api/orders/shipping-options` return them; the quote's `shippingOption` too.
- **FR-003**: `Shipping:Options:N:MinDays/MaxDays` seed new options only, validated at startup like the prices.
- **FR-004**: Checkout shows the estimate under each option and a Payment card; the stand-in line depends on Payment's own report.
- **FR-005**: `/admin/delivery` edits the estimate with the option.

## Success Criteria *(mandatory)*

- **SC-001**: A customer at checkout can tell from the page alone how long each option takes and when and how they will be charged.
- **SC-002**: The words "no money is moved" appear exactly while Payment says its provider is a stub (Vitest both ways).

## Assumptions

- Days are business days, one estimate for every destination - the shop has one carrier and ships within one country.
- No order shows an estimate after it is placed; the parcel's own tracking takes over from there.
