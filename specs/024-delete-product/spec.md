# Feature Specification: Delete a Product

> Written on 2026-09-27, after the feature merged (#61), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature Branch**: `024-withdraw-a-product` (the directory is named for what merged: deletion, not withdrawal)

**Created**: 2026-09-22

**Status**: Merged as [#61](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/61) on 2026-09-22

**Input**: The catalogue held 97 products that should never have existed - one per run of
`verify-saga.sh`, `verify-auth.sh` and Bruno, none of which cleaned up - against 14 real cameras, and
there was no way to remove them through the API at all. Cleaning meant raw SQL against Catalog's
database.

## Context

A listing sorted cheapest-first in dollars was a wall of `$0.01` widgets. Deactivating those products
would hide them from sale but leave them in every administrator's list, and it is the wrong tool
anyway: deactivation exists so that a cart holding the product can still explain itself and a report
still balances. These rows were never real. The shop needed a way to remove a product **for good**,
through the same authenticated API a person uses, and it needed the other services that count
something about that product to forget it too.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An administrator removes a product that should never have existed (Priority: P1)

An administrator finds a product that is test debris or a mistake and deletes it. It stops existing:
it is not listed, its address answers "not found", and its SKU can be used again.

**Why this priority**: This is the whole feature. Without it the only way to clean the catalogue is
SQL against another service's database, which the constitution forbids for anything but a one-off
recovery and which bypasses every rule the API enforces.

**Independent Test**: Create a product with two variants, a translation and a dollar price; delete
it; observe that `GET /api/products/{id}` is 404 and no variant, option, price or translation row
for it remains.

**Acceptance Scenarios**:

1. **Given** a product with two variants, **When** an administrator sends `DELETE /api/products/{id}`,
   **Then** the answer is 204 and the product, both variants, their options, their prices and the
   product's translations are gone.
2. **Given** a product has been deleted, **When** anyone asks for it, **Then** the answer is 404 - not
   a product marked inactive.
3. **Given** a product has been deleted, **When** an administrator creates a new product with the same
   SKU, **Then** it is accepted.
4. **Given** no product has the id, **When** an administrator deletes it, **Then** the answer is 404,
   so a script cannot report cleaning up something it never found.

---

### User Story 2 - Stock for a deleted product does not stay on the shelf (Priority: P2)

When the catalogue forgets a product, Inventory forgets the stock it was counting for every variant
of it.

**Why this priority**: Without it the count outlives the product: `GET /api/stock/{id}` keeps
answering for something no catalogue has heard of, and whatever was on hand stays there for ever.
It is second because it only matters once deletion exists.

**Independent Test**: Stock a product with two variants, delete the product, and observe that
`GET /api/stock/{variantId}` answers 404 for both within seconds.

**Acceptance Scenarios**:

1. **Given** a product with a body-only variant and a kit variant, both stocked, **When** the product is
   deleted, **Then** the stock rows for **both** variants are removed - not only the one whose id
   equals the product's.
2. **Given** the deletion message is delivered twice, **When** Inventory processes the second copy,
   **Then** nothing changes and nothing fails.
3. **Given** another product is stocked, **When** a different product is deleted, **Then** the other
   product's count is untouched.

---

### User Story 3 - A developer cleans a test-soiled catalogue in one command (Priority: P3)

A developer who has been running the end-to-end scripts against their local stack removes everything
that is not one of the seeded cameras, seeing what would go before anything goes.

**Why this priority**: It is the reason the endpoint was needed, but it is a tool on top of the
endpoint, not the endpoint itself.

**Independent Test**: Run `python seed/clean-test-debris.py` and read the list; run it again with
`--yes` and observe that only the products named in `seed/cameras.json` remain.

**Acceptance Scenarios**:

1. **Given** a catalogue with 14 seeded cameras and some debris, **When** the cleaner runs without
   `--yes`, **Then** it prints how many it would keep and delete and deletes nothing.
2. **Given** the same catalogue, **When** the cleaner runs with `--yes`, **Then** every product whose SKU
   is not in `cameras.json` is deleted through the API, and it reports how many were deleted and how
   many are left.

---

### Edge Cases

- **A customer or anonymous caller tries to delete.** Refused (403 / 401) before anything is read.
  Deletion is the most destructive thing the catalogue API can do.
- **A product has orders.** The orders are unaffected: each froze the name, price, SKU and option
  summary of what it bought (specs/009, 020, 021, 022). An order that changed because a catalogue row
  was deleted would be the defect.
- **A product sits in somebody's cart.** Cart is not told. The cart already shows a line whose variant
  Catalog no longer has as `NoLongerAvailable` (specs/010), and checkout's pricing call refuses an order
  naming a variant Catalog does not have.
- **A product deleted before it was ever stocked, or a redelivered deletion.** Inventory finds no row
  to remove; zero is a normal answer, not a failure.
- **Variants added after the first.** They do not share the product's id (specs/020), so the
  announcement must carry every variant id or the kit's units stay on the shelf for ever.
- **Stock reservations for a deleted product's variants.** Not removed by this feature; see
  [data-model.md](./data-model.md). A product nobody can order any more gains no new ones.
- **Stopping sale of a real product.** Not this feature. That is deactivation, which keeps the row.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let an administrator remove a product, and every variant of it, from the
  catalogue permanently.
- **FR-002**: Deletion MUST be refused to anybody who is not an administrator.
- **FR-003**: Deleting a product that does not exist MUST be reported as not found, not as success.
- **FR-004**: Deletion MUST remove everything that described the product - its variants, their options
  and per-currency prices, and its translations - so that nothing refers to it afterwards and its SKU
  can be reused.
- **FR-005**: Deletion MUST NOT change any order. Orders describe what was bought from their own frozen
  copies.
- **FR-006**: The deletion and the announcement that it happened MUST commit together: no deleted
  product without an announcement, no announcement for a product still there.
- **FR-007**: The announcement MUST name every variant of the deleted product.
- **FR-008**: Inventory MUST drop the stock count for every variant named, and processing the same
  announcement twice MUST have the same effect as processing it once.
- **FR-009**: A developer tool MUST be able to remove every product that is not a seeded camera, going
  through the API as an administrator, and MUST do nothing destructive unless explicitly confirmed.

### Key Entities

- **Product and its variants**: the rows removed. The product carries the name, description, category
  and image; each variant carries a SKU, price, options and per-currency prices (specs/020, 022).
- **Stock item** (Inventory): one count per variant id, dropped when the catalogue announces the
  variant deleted.
- **Product deleted announcement**: which product went, which variants went with it, and when.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A deleted product answers 404 at its address, and no row describing it remains in
  Catalog's database.
- **SC-002**: A deleted product's variants answer 404 at `GET /api/stock/{id}` within seconds of the
  deletion. The PR measured a probe product answering 404 four seconds after deletion.
- **SC-003**: A customer's attempt to delete the collection's real product is refused with 403 on every
  Bruno run.
- **SC-004**: The cleaner, run against the development catalogue, leaves exactly the products that
  `cameras.json` names. The PR recorded 97 deleted and 14 left.
- **SC-005**: Replaying the deletion announcement changes nothing (asserted by
  `Forgetting_twice_is_a_no_op_because_a_message_redelivers`).

## Assumptions

- Deletion is the rare operation, for rows that should never have existed. Taking a real product off
  sale remains deactivation.
- Only administrators delete. Sellers did not exist yet (they arrived in specs/027).
- Orders keep their own copies of what was bought and need no message.
- The cleaner is for development catalogues only; it deletes products and says so.

## Out of Scope

- Deleting the product's image file (a later gap: specs/029 closed it).
- Deleting categories. `DELETE /api/categories/{id}` merged in the next PR, #62, and is recorded under
  [specs/025](../025-storefront-redesign/). Its code comments call it "specs/024" as this feature's
  companion, and docs/features/catalog.md lists it under specs/024 too; the endpoint did not exist at
  this merge.
- Undo, soft delete, or an archive of deleted products.
- Telling Cart or Order: Cart already marks a missing variant, Order already froze what it sold.
- Cleaning up the stock rows that deletions made **before** the Inventory consumer was registered
  (see [research.md D5](./research.md)).
