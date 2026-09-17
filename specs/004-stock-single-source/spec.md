# Feature Specification: One Source of Truth for Stock

**Feature Branch**: `004-stock-single-source`

**Created**: 2026-09-17

**Status**: Draft

**Input**: Issue [#4](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/4) — "product listing shows a stock number that never changes"

## Why this exists

The product listing tells shoppers how many units are in stock. That number was typed in when the
product was created and has never been updated since — there is no code path that can change it. The
service that actually holds stock, moves it under concurrency, and reserves it during checkout is a
different one, and the two numbers were never related.

In the feature 003 end-to-end run the listing said **50** while the real figure went from 10 to 8.
Nothing was broken, in the sense that nothing threw. The listing simply said something untrue, to
anyone, without a token.

This is the situation the project's first principle is written against: exactly one service owns any
given fact, and a duplicated copy must never inform a decision. A shopper deciding whether to buy is
making exactly that decision.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The listing stops claiming a number it cannot know (Priority: P1)

A shopper browsing products sees whether each one can be bought — and that answer comes from
whoever actually holds the stock, not from a figure typed in months ago.

**Why this priority**: This is the defect. Everything else in this feature exists to keep this
answer true over time; this story is what makes it true at all.

**Independent test**: Browse the product listing and compare each product's availability against the
stock service's own figure for the same product. They agree, or the listing shows nothing about
stock.

**Acceptance Scenarios**:

1. **Given** a product whose owner reports units available, **When** a shopper views the product
   listing, **Then** it shows the product as available to buy.
2. **Given** a product whose owner reports no units available, **When** a shopper views the listing,
   **Then** it shows the product as unavailable.
3. **Given** any product, **When** the listing's answer is compared with the stock owner's, **Then**
   they agree — there is no product for which the two disagree.
4. **Given** a product the stock owner has never reported on, **When** a shopper views it, **Then**
   it shows as unavailable rather than available.

---

### User Story 2 - Selling the last unit changes what shoppers see (Priority: P2)

Stock moves — an order is placed, an order completes, an administrator restocks. The listing follows.

**Why this priority**: A listing that is correct once and then freezes is the bug this feature
exists to remove, just with a better initial value. P1 makes the answer right; P2 keeps it right.

**Independent test**: Note a product's availability, buy its remaining units through checkout, and
re-read the listing without touching it by hand.

**Acceptance Scenarios**:

1. **Given** a product with one unit available, **When** a shopper completes an order for it,
   **Then** the listing shows it as unavailable within seconds.
2. **Given** a product showing as unavailable, **When** an administrator adds stock, **Then** the
   listing shows it as available within seconds.
3. **Given** a product whose units are all held by in-flight orders, **When** a shopper views it,
   **Then** it shows as unavailable — held units are not available to somebody else.
4. **Given** a held order that fails and returns its units, **When** the listing is re-read, **Then**
   the product is available again.
5. **Given** the same stock announcement delivered twice, **When** it is processed, **Then** the
   listing is unchanged by the second delivery.
6. **Given** two announcements about one product arriving out of order, **When** both are processed,
   **Then** the listing reflects the later one, not whichever arrived last.

---

### User Story 3 - Creating a product no longer asks for a number it cannot keep (Priority: P3)

An administrator creating a product is not asked for a stock quantity by the service that does not
own stock.

**Why this priority**: Third because the listing is already correct after P1 and P2. But leaving the
field in place means the system keeps accepting a value it silently discards, which is how the
original defect reads to anyone who looks at it — an input that appears to do something and does
not.

**Independent test**: Create a product, then check that no stock figure was recorded anywhere except
by the stock owner.

**Acceptance Scenarios**:

1. **Given** an administrator creating a product, **When** they submit it, **Then** they are not
   asked for and cannot supply a stock quantity.
2. **Given** a newly created product, **When** a shopper views the listing, **Then** it shows as
   unavailable until stock is actually recorded for it.
3. **Given** a newly created product, **When** an administrator records stock for it through the
   stock owner, **Then** the listing shows it as available.

---

### Edge Cases

- **A product nobody has reported stock for.** Shows as unavailable. Erring towards "cannot buy" is
  recoverable; erring towards "in stock" sells goods that do not exist.
- **Announcements arriving out of order.** The network does not promise order. The listing must end
  up reflecting the most recent *observation*, not the most recent *delivery*.
- **The same announcement arriving twice.** Normal operation, not an error.
- **An announcement about a product the listing does not hold.** Discarded without error and without
  endless retry, and recorded where an operator can see it.
- **Every unit held by in-flight orders.** Available is what matters, not what is on the shelf.
- **The stock owner is unreachable for a while.** The listing keeps showing its last known answer
  and catches up when announcements resume; it must not fail, and must not silently flip everything
  to available.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Exactly one service MUST be the source of a product's stock figure, and it MUST be the
  service that holds and reserves the stock.
- **FR-002**: The product listing MUST NOT report a stock figure that it derived or stored
  independently of that owner.
- **FR-003**: The product listing MUST show whether a product can be bought, based on what the owner
  has announced.
- **FR-004**: The owner MUST announce a product's availability whenever it changes — through
  reservation, release, confirmation, or an administrator's adjustment.
- **FR-005**: A product with no announcement on record MUST read as unavailable.
- **FR-006**: Processing the same announcement twice MUST leave the listing unchanged.
- **FR-007**: An announcement older than the one already recorded for a product MUST NOT overwrite
  it.
- **FR-008**: An announcement about an unknown product MUST be discarded without error and without
  indefinite redelivery, and the fact MUST be visible to an operator.
- **FR-009**: Product creation MUST NOT accept a stock quantity.
- **FR-010**: The listing MUST reflect a change in availability within 10 seconds of the change
  being committed by the owner, under normal operation.
- **FR-011**: Availability MUST be computed from units a new shopper could actually buy — units held
  for other orders do not count.
- **FR-012**: The listing MUST remain readable, showing its last known answer, while the owner is
  unreachable.

### Key Entities

- **Product**: What is for sale — name, description, price, category. After this feature it also
  carries a **known availability**, which is a *record of what the owner last said*, not a fact the
  product owns.
- **Stock**: Units of a product: how many are on hand, how many are held for orders in flight, and
  how many a new shopper could buy. Owned by one service and by nothing else.
- **Availability announcement**: The owner's statement that a product's buyability has changed, and
  when it observed that.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Across every product in the catalogue, the listing's availability agrees with the
  stock owner's figure — 0 disagreements.
- **SC-002**: Buying the last available unit of a product changes the listing to unavailable within
  10 seconds, with no manual step.
- **SC-003**: Recording stock for a product with none changes the listing to available within 10
  seconds.
- **SC-004**: Delivering the same announcement 10 times leaves the listing identical to after the
  first.
- **SC-005**: Delivering two announcements for one product in reverse order leaves the listing
  showing the later observation.
- **SC-006**: No response from any service other than the stock owner contains a stock quantity.
- **SC-007**: A product created and never stocked reads as unavailable, 100% of the time.

## Assumptions

- **The listing shows a state, not a count.** The user chose on 2026-09-17 to expose whether a
  product can be bought rather than how many remain. A count that is three seconds stale reads as a
  bug and invites a shopper to plan around a number that is not theirs to rely on; "in stock" that
  is three seconds stale usually does not. Exposing exact counts publicly is also a business
  decision nobody has taken.
- **Product creation stops accepting a stock quantity** (the user's decision on 2026-09-17, FR-009).
  An administrator therefore creates the product and then records its stock with the owner — two
  steps, but the second is now visibly required rather than silently optional. The alternative
  considered and rejected was carrying an opening quantity along with the product's creation
  announcement, which would have meant the catalogue telling the stock owner what its stock is.
- **This is a breaking change for anything reading the product API.** The stock quantity leaves the
  product payload and the creation request. There is no frontend in this repository, so the blast
  radius is the API surface itself.
- **Availability is binary.** No "low stock" threshold — that is a merchandising decision with a
  number attached, and nobody has chosen the number.
- **No history.** The listing records the current answer, not a series of past ones.
- **Existing products keep working.** No product is deleted or made unbuyable by this change beyond
  what its true stock already implies.

## Dependencies

- The stock owner already computes what a new shopper could buy, and already exposes it publicly per
  product. What it does not do is announce a change — verified, not assumed: it publishes only
  reservation-succeeded and reservation-failed messages today, neither of which carries a level.
- The catalogue already receives an announcement when a product is created, and the stock owner
  already listens for it. That path exists; this feature adds the reverse direction.

## Out of scope

- A "low stock" indicator, back-in-stock notifications, or reserving stock from the listing.
- Backfilling availability for products created before this feature, beyond whatever the owner
  announces once it starts announcing.
- Any change to how stock is reserved, released or confirmed during checkout.
