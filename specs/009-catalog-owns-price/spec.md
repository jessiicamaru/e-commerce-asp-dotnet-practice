# Feature Specification: The Shop Decides What Things Cost

**Feature Branch**: `009-catalog-owns-price`

**Created**: 2026-09-21

**Status**: Draft

**Input**: Issue [#18](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/18) — "the customer sets the price, and the system charges it"

## Why this exists

A customer can buy anything for any price they choose.

The amount charged and the name recorded on an order line both come from the customer's own request.
Nothing asks the catalogue what the item costs. Reproduced against the running system on
2026-09-21:

```text
catalogue price: 40,000,000
stock before:    5
order total      1                  ← what the customer asked to pay
item name        Free Laptop lol    ← and what they asked it to be called
final status     Completed after 2s
stock after:     4                  ← gone
```

The payment was recorded and approved at `1.00`.

**No test catches this.** All sixty-one pass, and the reason is worse than it first looked.

The original reading was that each test supplies its own price and asserts against that same number,
verifying arithmetic rather than authority. That was wrong, and checking it was worth the minute:
**no test exercises order submission at all.** `grep -rln SubmitOrder tests/` returns nothing. The
fifteen Order tests cover reading orders back (six) and settling them (nine), and they build
`OrderItem` entities directly rather than going through the handler.

So the path that creates an order and decides what it costs has **zero** coverage, and the remedy is
not better-shaped tests for that path — it is that the path has none.

### The same mistake, twice

An order once carried the customer's identity in its request body, which let any caller order as any
user. That is why the project's governing principles now say identity comes from the validated token
and never from the request — and why that rule is marked absolute, with the reason:

> The rule is absolute because the mistake looks completely ordinary in review.

Identity was fixed. **Money was not.** The customer asserting the price is the same defect in the
same place, on the thing a shop exists to get right.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A customer pays what the shop charges (Priority: P1)

Somebody places an order. The amount they are charged is the amount the shop is asking for that
item, regardless of what their request said.

**Why this priority**: It is the entire feature and it closes a live hole. Everything else here
exists to make this trustworthy rather than merely usual.

**Independent Test**: Order a product listed at one price while claiming a different one, and check
what is charged.

**Acceptance Scenarios**:

1. **Given** a product the shop lists at 40,000,000, **When** a customer orders it claiming it costs
   1, **Then** they are not charged 1. The order is either refused or created at 40,000,000.
2. **Given** a product the shop lists at 40,000,000, **When** a customer orders it without claiming
   any price at all, **Then** the order is created at 40,000,000.
3. **Given** an order line, **When** it is read back, **Then** the product name on it is the shop's
   name for that product, not a name the customer supplied.
4. **Given** a request that refers to a product the shop does not have, **When** it is submitted,
   **Then** it is refused — not accepted with whatever the customer said about it.

---

### User Story 2 - What somebody bought does not change afterwards (Priority: P1)

A price changes in the catalogue. Orders placed before the change still say what was actually paid.

**Why this priority**: Equal first, and it is the half that is easy to miss. Fetching the right
price and then *storing a reference to it* would look correct on the day and quietly rewrite
history later. An order line is a record of a transaction, not a pointer to a current price.

**Independent Test**: Place an order, change the catalogue price, read the order back.

**Acceptance Scenarios**:

1. **Given** a completed order, **When** the catalogue price of an item on it changes, **Then** the
   order's line price and total are unchanged.
2. **Given** a completed order, **When** the product is renamed in the catalogue, **Then** the order
   still shows the name it was bought under.
3. **Given** a completed order, **When** the product is withdrawn from the catalogue entirely,
   **Then** the order is still readable in full.

---

### User Story 3 - The hole cannot reopen without something noticing (Priority: P1)

Somebody, later, adds a way for a price to come from a request again. It is caught before it merges.

**Why this priority**: Equal first, and it is the only story that is about *time*. This defect
survived because the path it lives on has no automated test of any kind — see above. Fixing the code
without fixing that absence leaves the next person free to reintroduce it exactly as innocently.

**Independent Test**: Deliberately accept a fabricated price and confirm the automated checks go red.

**Acceptance Scenarios**:

1. **Given** a system that accepts a fabricated price, **When** the automated checks run, **Then**
   they fail and say what was violated.
2. **Given** a working system, **When** the same checks run, **Then** they pass — a check that is
   red either way proves nothing.
3. **Given** the request a customer sends, **When** it is inspected, **Then** it has no field for a
   price or a name at all. A field the system ignores invites somebody to start honouring it.

---

### Edge Cases

- **The shop is unreachable when somebody tries to buy.** Today the order is placed at the wrong
  price; afterwards it cannot be placed at all. That is the right trade for a decision about money
  and it makes checkout depend on something it did not depend on before.
- **The shop is slow rather than unreachable.** A customer waiting on a slow lookup is a customer
  waiting on checkout, so there has to be a point at which waiting stops.
- **The price changes between the customer seeing it and submitting.** Whatever happens, the
  customer must not be charged more than they were shown without being told.
- **An order with several lines, one of which refers to a product that is gone.** Part-accepting an
  order is worse than refusing it.
- **The product exists but is withdrawn from sale**, or has no price set. "Exists" and "can be
  bought" are different questions.
- **The same request submitted twice.** The existing guarantee that a repeated submission does not
  charge twice must survive a lookup being added in front of it.
- **A product priced at zero**, if the catalogue permits one. Free is a legitimate price and must
  not be mistaken for a missing one.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The price charged for an order line MUST come from the shop's own record of that
  product, never from the request.
- **FR-002**: The product name recorded on an order line MUST come from the same place.
- **FR-003**: The customer's request MUST NOT contain a price or a product name. Not ignored —
  absent.
- **FR-004**: The price and name obtained MUST be stored on the order line as they were at the
  moment of purchase.
- **FR-005**: A later change to a product's price or name MUST NOT alter any existing order.
- **FR-006**: An order referring to a product the shop does not have MUST be refused in full.
- **FR-007**: An order referring to a product that cannot currently be sold MUST be refused, and the
  reason MUST be distinguishable from "no such product".
- **FR-008**: The order's total MUST be computed from the obtained prices, never from anything the
  request supplied.
- **FR-009**: When the shop cannot be reached, the order MUST be refused rather than placed on a
  guess.
- **FR-010**: Waiting for the shop MUST be bounded, and exceeding that bound MUST be reported as
  such rather than as the product not existing.
- **FR-011**: A transient failure to reach the shop MUST be retried a bounded number of times before
  the order is refused.
- **FR-012**: An automated check MUST fail if a fabricated price is ever accepted again, and MUST be
  demonstrated failing against a system that accepts one.
- **FR-013**: The existing guarantee that a repeated submission does not produce a second charge
  MUST be unaffected.

### Key Entities

- **A listed product**: What the shop says exists, what it is called, and what it costs. One owner,
  and that owner is not the order.
- **An order line**: What somebody bought, at the price they bought it at. A **record**, not a
  reference — this is the distinction US2 turns on.
- **A price lookup**: The act of asking the shop, at the moment of sale, what something costs. It
  can succeed, say "no such product", say "not for sale", or fail to answer — and those four are
  different outcomes, not three plus an error.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Ordering a 40,000,000 item while claiming it costs 1 results in **0** orders charged
  at 1.
- **SC-002**: The price on every order line equals the shop's price for that product at the moment
  the order was placed — **0 tolerance**.
- **SC-003**: Changing a catalogue price alters **0** existing orders.
- **SC-004**: An order naming a product the shop does not have creates **0** orders.
- **SC-005**: Deliberately accepting a fabricated price makes the automated checks fail; with the
  system intact they pass. **Both directions demonstrated.**
- **SC-006**: A customer's request carries **no** price field and **no** name field.
- **SC-007**: When the shop cannot answer, **0** orders are created, and the reported reason names
  the lookup rather than the product.
- **SC-008**: Checkout stays fast enough that nobody changes their behaviour because of it; the
  before and after are recorded rather than assumed.

## Assumptions

- **The shop is asked at the moment of purchase, not earlier.** There is nowhere to hold a price
  earlier — a cart does not exist
  ([#19](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/19)). A cart that
  holds the price the customer was shown is the better long-term shape and this feature must not
  wait for it: a live hole that lets somebody buy a 40,000,000 item for 1 is not a good reason to
  build a shopping basket first.
- **The mechanism is a synchronous call to the shop's own service**, chosen partly for practice.
  This is the **first** such call in the system — everything today is messages. Rejected: giving the
  order service its own copy of prices, fed by events. That copy would be seconds behind, and this
  project has already established that a message-fed copy may never make a sell decision; repeating
  it for money would be the same mistake a second time.
- **The customer is charged the price at submission**, not a price they were quoted earlier, because
  nothing quotes a price earlier yet. This assumption expires the day a cart exists.
- **"Cannot be sold" covers being withdrawn or inactive**, as the catalogue already records that.
  Stock is a different question and is already answered elsewhere, under a lock, at reservation
  time.
- **Currency is single and implicit**, as it is today. Nothing here makes that worse; nothing here
  fixes it.

## Dependencies

- The shop already has a public way to be asked about one product, and already owns the price.
- Checkout already works end to end and is already verified automatically, so there is an existing
  place for FR-012's check to live rather than a harness to build.

## Out of scope

- **A cart**, and anything that depends on one.
- **Quoting a price and honouring it later.** Requires a cart to have somewhere to put the quote.
- **Order totals beyond the sum of the lines** — shipping, tax, discounts
  ([#21](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/21)).
- **Changing how stock is checked.** Reservation already asks the owner under a lock and is correct.
- **Anything about currency.**
