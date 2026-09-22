# Feature Specification: The Shop Is a Marketplace

**Feature Branch**: `027-seller-accounts` · **Created**: 2026-09-23 · **Status**: Draft

**Input**: the owner asked for real sellers — people who list what they sell — with the administrator
managing the platform rather than stocking it.

## Why this exists

There are two kinds of account and one of them does everything.

An administrator today creates products, prices them, translates them, uploads their images and
withdraws them. That is not an administrator; that is the only shopkeeper. The shop has no way to
express the sentence **"this camera is sold by that person"**, and no way to stop one person editing
another's listing, because there is nobody else to be.

The storefront already says **"Sold by The shop"** on every card. It was laid out that way on purpose
in specs/025, against this feature, and it is currently a hard-coded string standing where a fact
should be.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A seller lists what they sell (Priority: P1)

Somebody registers as a seller with a shop name, and can immediately create products that are theirs.

**Why this priority**: it is the whole feature. Without it there is no second kind of participant.

**Independent Test**: register as a seller, create a product, and read it back — it names that shop.

**Acceptance Scenarios**:

1. **Given** a visitor, **When** they register as a seller with a shop name, **Then** they get an
   account that can list products, and their shop name is recorded.
2. **Given** a seller, **When** they create a product, **Then** the product records who sells it.
3. **Given** any shopper, **When** they read that product, **Then** it says which shop sells it —
   without the storefront having to ask a second service per card.

---

### User Story 2 - A seller may only touch their own listings (Priority: P1)

A seller can edit, price, translate, stock and withdraw their own products, and cannot do any of
those things to anybody else's.

**Why this priority**: the correctness half, and the reason ownership is worth recording at all. A
marketplace where any seller can re-price any listing is not a marketplace.

**Independent Test**: two sellers, one product each; each attempt by one on the other's is refused.

**Acceptance Scenarios**:

1. **Given** a seller and their own product, **When** they change its price, name, translation, image
   or availability, **Then** it is allowed.
2. **Given** a seller and **somebody else's** product, **When** they attempt any of those, **Then** it
   is refused as **not found** — never as "forbidden", which would confirm the product exists and
   whose it is.
3. **Given** a seller, **When** they ask for their own listings, **Then** they see theirs and only
   theirs.

---

### User Story 3 - The administrator manages the platform, not the stock (Priority: P2)

An administrator can still intervene on anything — that is what moderation is — but is no longer the
person expected to list products.

**Why this priority**: it is the owner's stated intent, and it decides what Admin means from here on.

**Acceptance Scenarios**:

1. **Given** an administrator, **When** they withdraw or edit any seller's product, **Then** it is
   allowed, because moderation is their job.
2. **Given** an administrator, **When** they create a product, **Then** it is allowed and belongs to
   the shop itself rather than to a seller — the shape every existing product has.

---

### User Story 4 - Everything that exists keeps working (Priority: P1)

The 14 products in the catalogue today have no seller, and must keep being sold.

**Acceptance Scenarios**:

1. **Given** a product created before this feature, **When** it is read, **Then** it reports the shop
   itself as its seller rather than an empty space or an invented name.
2. **Given** an order placed before or after, **Then** nothing about it changes: an order froze what
   it bought and does not care who sold it.

---

## Requirements *(mandatory)*

- **FR-001**: A visitor MUST be able to register as a seller, supplying a shop name, and MUST be able
  to list products immediately afterwards.
- **FR-002**: Every product MUST record which seller it belongs to, or record that it belongs to the
  shop itself. Existing products belong to the shop itself.
- **FR-003**: A read of a product MUST carry the seller's shop name **without a per-product call to
  another service** — a listing of 24 products must not become 24 lookups.
- **FR-004**: A seller MUST be refused any write to a product that is not theirs, and the refusal MUST
  be indistinguishable from the product not existing.
- **FR-005**: An administrator MUST be able to write to any product.
- **FR-006**: A seller MUST be able to list their own products, and that listing MUST contain only
  theirs.
- **FR-007**: A shopper MUST be able to see who sells a product before buying it.
- **FR-008**: Changing a shop's name MUST change what the catalogue shows, without editing products.

## Success Criteria *(mandatory)*

- **SC-001**: Two sellers, and every cross-write between them is a 404 — asserted per operation, not
  once.
- **SC-002**: A product listing page shows shop names with no increase in cross-service calls.
- **SC-003**: Every product that existed before this feature still reads, sells and checks out.
- **SC-004**: Renaming a shop changes the name on its products with no write to any product.

## Assumptions

- **A seller is usable the moment they register.** No approval queue. This is the owner's shop and a
  practice project; a real marketplace holds a seller pending until somebody checks who they are, and
  that is recorded here as deliberately not built rather than overlooked. *Chosen on the owner's
  behalf when the question went unanswered — see the note in the plan.*
- One shop per account. A person selling under two names registers twice.
- A seller is also a shopper: the same account can buy.
- Withdrawing a seller, suspending one, and what happens to their listings when that happens, are out
  of scope. The shop has no way to do it to a customer either.

## Out of scope

- A seller's management console. The API is the surface; the storefront gets the shop **name** and a
  way to register, not a listings dashboard. Recorded because it is the obvious next thing.
- Payouts, commission, settlement — there is no money movement in this system at all (Payment is a
  stub that approves without contacting anybody).
- Per-seller delivery options, per-seller policies, seller ratings.
- Splitting one order across two sellers' stock into two shipments. An order today is one shipment
  from one warehouse, and pretending otherwise would be a lie in the data.

## Decisions taken before building

1. Where the shop name lives, and how Catalog shows it without asking Identity per product.
2. How a seller registers, and what the token then carries.
3. What "not yours" returns, and on which operations.
4. What an existing product's seller is.
5. What the storefront shows, and what it does not yet.
