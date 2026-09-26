# Feature Specification: A seller can stock what they sell

> Completed on 2026-09-27, after the feature merged (#71), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Feature branch**: `031-seller-stock`
**Created**: 2026-09-23
**Status**: Merged (#71, 2026-09-23)
**Closes**: #70

## What is wrong

A seller can register, open a shop, list a product and set its price — and then **cannot sell it**.
The listing reads `OutOfStock` from the moment it exists and stays that way until an administrator,
who has never heard of her shop, types a number.

Reproduced end to end on the running stack:

```
a real seller, roles ['Seller', 'Customer']
she lists a product -> 200, availability = 'OutOfStock'
she tries to stock her OWN product:
  PUT /api/stock/{id} as the seller -> 403
```

specs/027 made this shop a marketplace and specs/028 gave sellers a console. Both are real, and
neither lets a seller complete the one transaction a shop exists for.

## Why it is harder than it looks

**Inventory has never heard of a seller.** Not one file in that service mentions one, and although
it validates tokens it has **no `ICurrentUser` anywhere** — it has only ever needed to know that the
caller holds `Admin` at the door, never *who* they are. This feature introduces caller identity into
a service that has never used it.

**And the answer lives in another service.** `SellerOwnership` reads `products.SellerId`, a *Catalog*
column. Inventory has no such column, and the id it is given is a **variant** id (specs/020), so the
question is really "who owns the product this variant belongs to?" — two hops, both inside Catalog.

## User Scenarios & Testing

### US1 - A seller stocks their own product (Priority: P1)

Alice lists a camera, says she has three, and a shopper buys one.

**Why this priority**: without it a seller's listing is unsellable for ever. It is the whole of issue
#70, and nothing else in the feature matters until it works.

**Independent Test**: list, stock, buy, and watch the stock fall by one.

**Acceptance Scenarios**:

1. **Given** a seller who owns a product, **When** she sets the quantity of one of its variants,
   **Then** it is accepted.
2. **Given** that product read `OutOfStock`, **When** its stock is set above zero, **Then** the listing
   stops reading `OutOfStock`, through the existing availability announcement.
3. **Given** the product is stocked, **When** a shopper checks out, **Then** the saga reserves and
   deducts exactly as before.
4. **Given** an administrator, **When** they set the stock of anything, **Then** it is accepted, as
   today.

---

### US2 - A seller cannot stock somebody else's (Priority: P1)

**Why this priority**: opening a write to a new role is only safe if the refusal is proven with it.
US1 without US2 would let any seller zero out a rival's shop.

**Independent Test**: two real seller tokens against the API; the second seller's write on the
first seller's variant is refused and the first seller's stock is unchanged.

**Acceptance Scenarios**:

1. **Given** a variant belonging to another seller, **When** a seller sets its stock, **Then** it is
   refused with **404**, never 403 — the same answer as a variant that does not exist, and the same
   answer specs/027 already gives.
2. **Given** that refusal, **When** the other seller's stock is read, **Then** it is unchanged.
3. **Given** the refusal must be real, **When** it is verified, **Then** it is proven against the API
   with two real tokens, not against the storefront.
4. **Given** a product of the shop's own (no seller), **When** a seller sets its stock, **Then** it is
   the same 404 — a seller cannot adopt a shop product by being the only one asking.

---

### US3 - The control is where a seller already is (Priority: P2)

**Why this priority**: the API alone is enough for US1 and US2, so the page comes second; but a
seller does not use curl, so without it SC-001 is not met by a person.

**Independent Test**: open `/shop/products/:id` as the seller, set a number, and see the server
accept it or refuse it in its own words.

**Acceptance Scenarios**:

1. **Given** a seller on `/shop/products/:id`, **When** the page loads, **Then** it carries the
   quantity beside the price editor.
2. **Given** some units are inside somebody's checkout, **When** the page shows stock, **Then** it
   shows what is currently on hand, and how many are held for orders in progress.
3. **Given** the server refuses, **When** the refusal is shown, **Then** it is shown in the server's
   own words.

---

### US4 - A newly listed product can be stocked (Priority: P2)

The stock row is created asynchronously, off the broker. Immediately after listing, it does not
exist.

**Why this priority**: it is a window of seconds, but it is the first thing every new seller does
after listing, and a bare 404 there reads as "not yours".

**Independent Test**: list a product and stock it immediately; the answer is either 200 or a 404
worded as "not registered in inventory", and a retry succeeds.

**Acceptance Scenarios**:

1. **Given** a seller's product whose stock row has not arrived yet, **When** she stocks it, **Then**
   she gets an answer that says so, and succeeds on a retry — not a bare 404 indistinguishable from
   "not yours".

---

### Edge Cases

- **The stock row has not arrived yet.** `ProductCreatedConsumer` creates it off the broker. The
  answer is a 404 worded differently from a refusal, reachable only after ownership has passed
  (research D6).
- **A reservation is in progress while the seller edits.** The write takes the same `FOR UPDATE`
  lock as the reserve path and refuses a value below `QuantityReserved` with 409 (research D5).
- **The shop's own products have no seller.** `products.SellerId` is null; only an administrator may
  stock them.
- **Catalog is unreachable.** The seller gets 503 — "could not find out" — never the 404 that means
  "not yours" (FR-009).
- **A customer with no Seller role.** Refused at the door with 403; no ownership read happens.

## Requirements

### Functional Requirements

- **FR-001** A seller MUST be able to set the quantity on hand of a variant belonging to a product
  they own.
- **FR-002** A seller MUST be refused, with **404**, for a variant they do not own.
- **FR-003** An administrator MUST keep the access they have today.
- **FR-004** The caller's identity MUST come from the token. No endpoint gains a seller id, in a
  body or a path.
- **FR-005** Ownership MUST be decided from the current state of Catalog, not from a copy that can
  be behind.
- **FR-006** The existing protections MUST survive untouched: the `FOR UPDATE` lock shared with the
  reserve path, the refusal to set below what is reserved, and the availability announcement.
- **FR-007** A seller MUST be able to see how many of their own units are held for orders.
- **FR-008** Every client change ships with tests.
- **FR-009** When ownership cannot be established because Catalog is unreachable, the answer MUST be
  a 503, not a 404. *(Added in this backfill from the code: `GrpcProductOwnership` throws
  `DependencyUnavailableException`, and `Catalog_being_unreachable_is_not_a_refusal` asserts it.)*

### Key Entities

- **Variant owner**: what Catalog reports about one variant — its id, its product's id, and the
  product's seller id, empty for the shop's own. A fact, not a decision; Inventory compares it to its
  caller.
- **Stock item**: unchanged from specs/001 and 004 — `stock_items`, keyed by variant id since
  specs/020. The feature writes the same row in the same way; only who may do so changes.

## Out of scope

- **A quantity field on the listing form.** The stock row does not exist yet at that moment
  (research D6); stocking stays a second step.
- **Deltas** (`+3`, `-1`). Absolute is deliberate and already guarded; research D5.
- **Stock history, low-stock alerts, reorder points.** A shop that cannot say "I have three" does
  not need a report about it yet.

## Success Criteria

- **SC-001** A seller lists a product, stocks it, and a shopper buys it — with no administrator
  involved at any point.
- **SC-002** A seller is refused another seller's variant with 404, proven against the API with two
  real tokens.
- **SC-003** `verify-saga.sh` still passes. This touches the service the saga reserves against.
- **SC-004** Inventory's 31 existing tests still pass.

## Assumptions

- Catalog being unreachable means a seller cannot stock. Acceptable: Catalog being unreachable also
  means nobody can see the product.
- One seller per product. `products.SellerId` is a single nullable column and nothing here changes
  that.
