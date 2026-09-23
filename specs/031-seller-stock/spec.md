# Feature Specification: A seller can stock what they sell

**Feature branch**: `031-seller-stock`
**Created**: 2026-09-23
**Status**: Draft
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

## User Scenarios

### US1 - A seller stocks their own product (P1)

Alice lists a camera, says she has three, and a shopper buys one.

**Independently testable**: list, stock, buy, and watch the stock fall by one.

**Acceptance**
1. A seller sets the quantity of a product that is theirs and it is accepted.
2. The listing stops reading `OutOfStock`, through the existing availability announcement.
3. A shopper can then check out, and the saga reserves and deducts exactly as before.
4. An administrator can still stock anything, as today.

### US2 - A seller cannot stock somebody else's (P1)

**Acceptance**
1. Setting stock on another seller's product is refused with **404**, never 403 — the same answer as
   a variant that does not exist, and the same answer specs/027 already gives.
2. The other seller's stock is unchanged.
3. This is proven against the API with two real tokens, not against the storefront.

### US3 - The control is where a seller already is (P2)

**Acceptance**
1. `/shop/products/:id` carries the quantity beside the price editor.
2. It shows what is currently on hand, and how many are held for orders in progress.
3. A refusal is shown in the server's own words.

### US4 - A newly listed product can be stocked (P2)

The stock row is created asynchronously, off the broker. Immediately after listing, it does not
exist.

**Acceptance**
1. A seller stocking a product whose row has not arrived yet gets an answer that says so, and
   succeeds on a retry — not a bare 404 indistinguishable from "not yours".

## Requirements

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
