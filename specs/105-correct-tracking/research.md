# Research: A mistyped tracking reference can be corrected

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #212

---

## D1 - Correctable until delivered, not after

**Decision**: A reference can be corrected while the part is `Shipped`, not yet delivered, and not cancelled. After
delivery the answer is 409.

**Rationale**: The reference exists so the buyer can follow the parcel. Once the buyer has confirmed it arrived, or the
7-day sweep has taken it as delivered, there is nothing left to follow. A change then could only rewrite history, for
example against a return that is under way.

**Alternatives considered**: allowing a correction at any time, rejected for the history reason; and allowing it only
within the first hours, rejected because a typo is often found by the buyer days later.

---

## D2 - No limit; a trail and a notice instead

**Decision**: There is no cap on corrections. Each one records `TrackingCorrected` in the audit log with the old and the
new reference, and tells the buyer.

**Rationale**: The issue worried that corrections could "hide a parcel". A cap would refuse a genuine second typo and
still allow the first abuse. What deters abuse is visibility: the buyer sees every change as it happens, and staff can
see every old reference in the audit log. The same reference again is a no-op, so a double click does not spam the
buyer.

---

## D3 - The shipping time stands

**Decision**: `ShippedAt` is not touched.

**Rationale**: `ShippedAt` is what automatic delivery counts 7 days from (specs/040), and what the seller's money waits
on through the return window (specs/066). A correction fixes a label; the parcel left when it left. Resetting the time
would let a seller delay the automatic confirmation, and with it the buyer's return window, by retyping a reference.
SC-004 mutates this.

---

## D4 - Under the order's lock, the summary rewritten

**Decision**: The same row lock as every parcel move. The order's own `TrackingReference`, which is the part's when the
order has exactly one part (specs/035), is rewritten by the same summary routine the moves use.

**Rationale**: A correction racing a customer's "received" must see one or the other, not a reference written after
delivery. Reusing the summary routine keeps the one-part rule in one place.
