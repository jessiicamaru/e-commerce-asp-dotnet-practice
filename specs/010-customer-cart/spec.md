# Feature Specification: Somewhere to Put What You Intend to Buy

**Feature Branch**: `010-customer-cart`

**Created**: 2026-09-21

**Status**: Draft

**Input**: Issue [#19](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/19) — "nothing holds what a customer intends to buy"

## Why this exists

A customer's only move is to submit a finished order. Whatever they were considering exists solely in
whichever client assembled the request, and vanishes the moment they close it. Come back tomorrow and
the shop has no memory they were ever choosing between two things.

The step every shop has between browsing and buying — accumulating things, changing your mind,
seeing a running total — is not missing an implementation. It is missing a place in the design.

## Decisions already taken

Two questions in #19 were marked *"should be a decision"*, and were put to the person who owns the
project rather than defaulted:

- **The cart is its own service.** A cart and an order have different lives: a cart changes
  constantly and lives for weeks; an order is an immutable record of a transaction. Keeping them
  apart follows the database-per-service model this project uses, and is how Microsoft's reference
  .NET microservices application (eShopOnContainers) models its basket.
- **Guest carts are deferred.** A cart belongs to a signed-in customer. A cart before sign-in needs an
  anonymous identity and a way to merge it on sign-in — enough for a feature of its own, and it sits
  awkwardly beside the rule that identity comes from the validated token. Deferred as a decision,
  not forgotten.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A customer keeps a cart (Priority: P1)

A signed-in customer puts things in a cart, changes quantities, removes things, and finds it exactly
as they left it when they come back.

**Why this priority**: It is the feature. Everything else here depends on a cart existing.

**Independent Test**: Add items, sign out, sign back in from somewhere else, and look.

**Acceptance Scenarios**:

1. **Given** a signed-in customer, **When** they add a product, **Then** it is in their cart with the
   quantity they chose.
2. **Given** a product already in the cart, **When** they add it again, **Then** the quantity grows —
   it does not appear twice.
3. **Given** a cart line, **When** the customer changes its quantity or removes it, **Then** the cart
   reflects that.
4. **Given** a cart with items, **When** the customer signs out and back in, from any device, **Then**
   the cart is as they left it.
5. **Given** a cart, **When** it is emptied, **Then** nothing is in it.

---

### User Story 2 - Checking out buys what is in the cart (Priority: P1)

The customer checks out. The order contains exactly what their cart contained, and the cart is
emptied — but only once the order has actually been accepted.

**Why this priority**: Equal first. A cart that cannot be bought from is a list. And the ordering of
the two effects is where this goes wrong in a way nobody notices: emptying the cart and then failing
to create the order loses what the customer chose, which is worse than never having a cart.

**Independent Test**: Fill a cart, check out, confirm the order matches and the cart is empty. Then
make the order fail and confirm the cart is untouched.

**Acceptance Scenarios**:

1. **Given** a cart with items, **When** the customer checks out, **Then** an order is created
   containing exactly those products and quantities.
2. **Given** a successful checkout, **When** the cart is read afterwards, **Then** the lines that were
   ordered are gone.
3. **Given** a checkout that is refused — unknown product, product not for sale, catalogue
   unreachable — **When** the cart is read afterwards, **Then** it is exactly as it was.
4. **Given** an empty cart, **When** the customer checks out, **Then** it is refused, and no order
   exists.
5. **Given** a customer who adds something *while* a checkout is in flight, **When** the order is
   accepted, **Then** the newly added item is still in the cart — only what was ordered is removed.

---

### User Story 3 - The cart never decides what is charged (Priority: P1)

A cart shows prices. The amount the customer pays is decided by the shop at the moment of purchase,
and if that differs from what the cart showed, the customer can see so.

**Why this priority**: Equal first, and it is the rule the previous feature existed to establish.
Feature 009 closed a hole where the customer set the price. A cart is a new place a price can be
held, and therefore a new place for exactly that mistake to come back — this time through a copy the
system made itself rather than one the customer supplied.

**Independent Test**: Put something in the cart, change its price in the catalogue, check out, and
see what is charged.

**Acceptance Scenarios**:

1. **Given** an item in the cart, **When** the customer views the cart, **Then** each line shows the
   shop's current price for that product.
2. **Given** an item whose price changed after it was added, **When** the customer checks out,
   **Then** they are charged the shop's current price — never a price the cart remembered.
3. **Given** a cart, **When** it is read, **Then** its total is presented as an estimate, not as a
   promise, because the charge is decided at checkout.

---

### User Story 4 - A cart is private (Priority: P2)

One customer's cart cannot be seen or changed by anybody else.

**Why this priority**: Second because US1–US3 deliver the feature. But a cart is keyed by who is
asking, and this project has already shipped a defect where "who is asking" came from the request
body.

**Independent Test**: Two customers, real signed tokens, each tries to read and change the other's
cart.

**Acceptance Scenarios**:

1. **Given** two customers with carts, **When** each reads their cart, **Then** each sees only their
   own.
2. **Given** a request that names another customer's cart in any way, **When** it is made, **Then**
   it has no effect on that cart.
3. **Given** an unauthenticated caller, **When** they try to use a cart, **Then** they are refused.

---

### Edge Cases

- **A product is withdrawn while it sits in a cart.** Silently dropping it is the worst option — the
  customer comes back to a cart that shrank without explanation. The line stays and is shown as
  unavailable; checkout is refused while it is there.
- **A product in the cart no longer exists at all.** Same treatment, distinguishable reason.
- **A product goes out of stock while in a cart.** A cart is **not** a reservation and holds no stock.
  Checkout finds out, exactly as it does today, when Inventory reserves under a lock.
- **Quantity of zero or less.** Rejected on add; setting a line to zero removes it.
- **A very large quantity.** A cart may express it; checkout and Inventory decide whether it can be
  satisfied. The cart does not second-guess stock.
- **Two devices changing the same cart at once.** The later change must not silently undo the
  earlier one.
- **The order is accepted but the cart cannot be updated straight away.** The cart must catch up
  rather than keep lines the customer has already bought.
- **The same order acceptance is announced twice.** The cart must not remove the lines twice, and
  must not remove lines added after the order.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A signed-in customer MUST have exactly one cart, identified from their validated
  identity and never from anything in the request.
- **FR-002**: A customer MUST be able to add a product with a quantity, change a line's quantity,
  remove a line, and empty the cart.
- **FR-003**: Adding a product already in the cart MUST increase that line's quantity rather than add
  a second line.
- **FR-004**: The cart MUST persist across sessions and devices.
- **FR-005**: Checkout MUST build the order from the caller's cart, not from a list the client
  supplies.
- **FR-006**: The lines that were ordered MUST be removed from the cart only after the order has been
  accepted, and only those lines.
- **FR-007**: A refused checkout MUST leave the cart exactly as it was.
- **FR-008**: Checking out an empty cart MUST be refused and create no order.
- **FR-009**: The price charged MUST be the shop's price at checkout. Any price the cart holds MUST NOT
  inform the charge.
- **FR-010**: A cart line MUST show the shop's current price, and the cart's total MUST be presented as
  an estimate.
- **FR-011**: A cart line whose product is withdrawn or gone MUST remain visible and marked, and
  checkout MUST be refused while it is present.
- **FR-012**: A cart MUST NOT hold, reserve or promise stock.
- **FR-013**: One customer MUST NOT be able to read or change another customer's cart.
- **FR-014**: Removing ordered lines MUST be safe to repeat: a second announcement of the same
  accepted order MUST change nothing.

### Key Entities

- **A cart**: What one customer intends to buy. One per customer, long-lived, freely changed.
- **A cart line**: A product and a quantity. May carry a *display* copy of a price, which is never
  what is charged.
- **An accepted order**: The signal that the lines it contained can leave the cart. Owned by the
  ordering service; the cart only learns of it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A cart filled, abandoned and returned to — from a different device, after signing out —
  shows **100%** of what was left in it.
- **SC-002**: A checkout creates an order containing **exactly** the cart's products and quantities —
  **0** additions, **0** omissions.
- **SC-003**: After a successful checkout, **0** of the ordered lines remain in the cart.
- **SC-004**: After a refused checkout, the cart is **identical** to before.
- **SC-005**: An item added during an in-flight checkout is **still in the cart** afterwards.
- **SC-006**: When the catalogue price changes between adding and checkout, the customer is charged
  the **new** price, every time — **0** checkouts at a price the cart remembered.
- **SC-007**: **0** successful reads or changes of another customer's cart, verified with real signed
  identities.

## Assumptions

- **Signed-in customers only**, by decision (see above).
- **The cart is its own service** with its own database, by decision (see above).
- **A cart holds no stock.** Inventory reserves under a row lock at checkout, as it does today. A cart
  that reserved stock would be a second reservation mechanism with its own expiry, which #19 warns
  against explicitly: *"a cart is not a reservation and must not start behaving like one."*
- **Abandoned carts do not expire in this feature.** Clearing stale carts is an operational concern
  with no customer-visible acceptance criterion yet; it is recorded, not built.
- **Checkout keeps re-pricing from the shop at submission**, which feature 009 built. The cart adds
  a place to *show* a price, not a new authority over one.

## Dependencies

- Feature 009: order submission already asks the shop for the price and refuses unknown or unsellable
  products. Checkout from a cart inherits all of that rather than re-implementing it.
- The ordering service already announces an accepted order, through its transactional outbox, in
  the same transaction that records it. That announcement is exactly the signal "the order was
  accepted, and only then."

## Out of scope

- Guest carts, and merging a guest cart on sign-in.
- Expiring abandoned carts.
- Saving items for later, wish lists, or sharing a cart.
- Quoting a price and honouring it later.
- Shipping, tax and totals beyond a sum of lines ([#20](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/20), [#21](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/21)).
